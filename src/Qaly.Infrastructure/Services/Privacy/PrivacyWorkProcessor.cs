using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.Privacy;

public interface IPrivacyWorkProcessor
{
    Task ProcessAsync(PrivacyWorkLease lease, string workerId, CancellationToken ct = default);
}

public sealed partial class PrivacyWorkProcessor : IPrivacyWorkProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly QalyDbContext _db;
    private readonly IPrivacyPayloadProtector _payloadProtector;
    private readonly IVectorStorageService _vectorStorage;
    private readonly PrivacyV4Options _options;

    public PrivacyWorkProcessor(
        QalyDbContext db,
        IPrivacyPayloadProtector payloadProtector,
        IVectorStorageService vectorStorage,
        IOptions<PrivacyV4Options> options)
    {
        _db = db;
        _payloadProtector = payloadProtector;
        _vectorStorage = vectorStorage;
        _options = options.Value;
    }

    public Task ProcessAsync(PrivacyWorkLease lease, string workerId, CancellationToken ct = default)
        => lease.Kind switch
        {
            PrivacyWorkKinds.Retention => ProcessRetentionAsync(lease, workerId, ct),
            PrivacyWorkKinds.DataSubjectRequest => ProcessDataSubjectRequestAsync(lease, workerId, ct),
            _ => throw new InvalidOperationException($"Unsupported privacy work kind '{lease.Kind}'.")
        };

    private async Task ProcessRetentionAsync(PrivacyWorkLease lease, string workerId, CancellationToken ct)
    {
        var action = await _db.PrivacyRetentionActions
            .Include(item => item.RetentionPolicy)
            .FirstOrDefaultAsync(item => item.Id == lease.WorkId, ct);
        if (action == null || action.Status != PrivacyWorkerStatuses.Running || action.LeaseOwner != workerId)
        {
            return;
        }

        var meeting = action.EntityType == nameof(MeetingImport)
            ? await _db.MeetingImports.FirstOrDefaultAsync(item => item.Id == action.EntityId, ct)
            : null;
        if (meeting == null)
        {
            CompleteRetentionAction(action, "entity_missing", DateTimeOffset.UtcNow);
            AddAudit(
                action.TenantId,
                action.ProjectId,
                null,
                "RETENTION_ENTITY_MISSING",
                action.EntityType,
                action.EntityId,
                action.RetentionPolicyId,
                action.RetentionPolicy.PolicyVersion,
                action.RetentionPolicy.Purpose,
                "completed",
                metadata: new Dictionary<string, string?> { ["actionType"] = action.ActionType });
            await _db.SaveChangesAsync(ct);
            return;
        }

        var held = await HasActiveLegalHoldAsync(
            action.TenantId,
            action.ProjectId,
            meeting.ImportedById,
            nameof(MeetingImport),
            meeting.Id,
            ct);
        if (held)
        {
            action.Status = PrivacyWorkerStatuses.LegalHold;
            action.LeaseOwner = null;
            action.LeaseExpiresAt = null;
            action.CompletedAt = DateTimeOffset.UtcNow;
            action.EvidenceJson = "{\"outcome\":\"legal_hold\"}";
            meeting.PrivacyState = MeetingPrivacyStates.LegalHold;
            AddAudit(
                action.TenantId,
                action.ProjectId,
                null,
                "RETENTION_BLOCKED_LEGAL_HOLD",
                nameof(MeetingImport),
                meeting.Id,
                action.RetentionPolicyId,
                action.RetentionPolicy.PolicyVersion,
                action.RetentionPolicy.Purpose,
                "legal_hold",
                PrivacyErrorCodes.LegalHold,
                new Dictionary<string, string?> { ["actionType"] = action.ActionType });
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (action.ActionType == PrivacyExpiryActions.Review)
        {
            meeting.PrivacyState = MeetingPrivacyStates.Expired;
            CompleteRetentionAction(action, "manual_review_required", DateTimeOffset.UtcNow);
            AddAudit(
                action.TenantId,
                action.ProjectId,
                null,
                "RETENTION_REVIEW_REQUIRED",
                nameof(MeetingImport),
                meeting.Id,
                action.RetentionPolicyId,
                action.RetentionPolicy.PolicyVersion,
                action.RetentionPolicy.Purpose,
                "review_required",
                metadata: new Dictionary<string, string?> { ["actionType"] = action.ActionType });
            await _db.SaveChangesAsync(ct);
            return;
        }

        await InvalidateVectorAsync(meeting.ProjectId, meeting.ImportedById);
        await EraseMeetingAndDerivedContentAsync(
            meeting,
            action.ActionType == PrivacyExpiryActions.Delete,
            ct);
        var now = DateTimeOffset.UtcNow;
        CompleteRetentionAction(action, action.ActionType, now);
        AddAudit(
            action.TenantId,
            action.ProjectId,
            null,
            action.ActionType == PrivacyExpiryActions.Delete ? "RETENTION_CONTENT_DELETED" : "RETENTION_CONTENT_REDACTED",
            nameof(MeetingImport),
            meeting.Id,
            action.RetentionPolicyId,
            action.RetentionPolicy.PolicyVersion,
            action.RetentionPolicy.Purpose,
            "completed",
            metadata: new Dictionary<string, string?> { ["actionType"] = action.ActionType });
        await _db.SaveChangesAsync(ct);
    }

    private async Task EraseMeetingAndDerivedContentAsync(
        MeetingImport meeting,
        bool markDeleted,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        meeting.TranscriptText = string.Empty;
        meeting.Summary = null;
        meeting.ParticipantsJson = "[]";
        meeting.RawPayloadJson = "{}";
        meeting.ContentRedactedAt ??= now;
        meeting.PrivacyState = markDeleted ? MeetingPrivacyStates.Deleted : MeetingPrivacyStates.Redacted;
        if (markDeleted)
        {
            meeting.ContentDeletedAt ??= now;
        }

        if (meeting.AiJobId.HasValue)
        {
            var job = await _db.AiJobs
                .Include(item => item.Drafts)
                .FirstOrDefaultAsync(item => item.Id == meeting.AiJobId.Value, ct);
            if (job != null)
            {
                WipeAiJob(job, now);
            }
        }

        var meetingImportIdStr = meeting.Id.ToString();
        var legacySourceId = $"meetily:{meeting.SourceId}";
        var sessions = await _db.GroupMeetingSessions
            .Where(session => session.TranscriptSourceId == meetingImportIdStr || session.TranscriptSourceId == legacySourceId)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.Summary = null;
            session.TranscriptSourceId = "[redacted]";
        }

        var projectCaches = await _db.AiPromptCache
            .Where(cache => cache.ProjectId == meeting.ProjectId)
            .ToListAsync(ct);
        _db.AiPromptCache.RemoveRange(projectCaches);
    }

    private static void WipeAiJob(AiJob job, DateTimeOffset now)
    {
        job.RequestJson = "{}";
        job.ResultJson = null;
        job.PolicyDecisionJson = JsonSerializer.Serialize(new
        {
            redactedAt = now,
            reason = "privacy_erasure",
            job.ConsentId,
            job.RetentionPolicyId
        }, JsonOptions);
        foreach (var draft in job.Drafts)
        {
            draft.PayloadJson = "{}";
            draft.OriginalPayloadJson = "{}";
            draft.WorkingPayloadJson = "{}";
            draft.ConfirmationResultJson = null;
            draft.ConfirmationNote = null;
            draft.RejectionReason = null;
            draft.WarningsJson = "[\"privacy_content_removed\"]";
            if (draft.Status == AiDraftStatuses.PendingReview)
            {
                draft.Status = AiDraftStatuses.Expired;
                draft.ExpiresAt = now;
            }
        }
    }

    private Task InvalidateVectorAsync(Guid projectId, Guid ownerId)
        => _vectorStorage.DeleteByFilterAsync(new VectorFilter
        {
            ProjectId = projectId,
            OwnerId = ownerId
        }, "qaly_context");

    private Task<bool> HasActiveLegalHoldAsync(
        Guid tenantId,
        Guid? projectId,
        Guid? subjectUserId,
        string entityType,
        Guid entityId,
        CancellationToken ct)
        => _db.PrivacyLegalHolds.AsNoTracking().AnyAsync(hold =>
            hold.TenantId == tenantId &&
            hold.Status == PrivacyLegalHoldStatuses.Active &&
            (!hold.ProjectId.HasValue || hold.ProjectId == projectId) &&
            ((hold.EntityType == entityType && hold.EntityId == entityId) ||
                (subjectUserId.HasValue && hold.SubjectUserId == subjectUserId)),
            ct);

    private static void CompleteRetentionAction(
        PrivacyRetentionAction action,
        string outcome,
        DateTimeOffset completedAt)
    {
        action.Status = PrivacyWorkerStatuses.Completed;
        action.LeaseOwner = null;
        action.LeaseExpiresAt = null;
        action.CompletedAt = completedAt;
        action.EvidenceJson = JsonSerializer.Serialize(new { outcome }, JsonOptions);
        action.LastErrorCode = null;
        action.LastErrorMessage = null;
    }

    private void AddAudit(
        Guid? tenantId,
        Guid? projectId,
        Guid? actorUserId,
        string eventType,
        string entityType,
        Guid entityId,
        Guid? policyId,
        string? policyVersion,
        string? purpose,
        string outcome,
        string? failureCode = null,
        Dictionary<string, string?>? metadata = null,
        Guid? dsarId = null)
    {
        _db.AiAuditEvents.Add(new AiAuditEvent
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ActorUserId = actorUserId,
            EventType = eventType,
            EntityType = entityType,
            EntityGuid = entityId,
            EntityKey = entityId.ToString(),
            RetentionPolicyId = policyId,
            DataSubjectRequestId = dsarId,
            Purpose = purpose,
            PolicyVersion = policyVersion,
            DataClassification = PrivacyDataClasses.SensitiveCollaboration,
            Outcome = outcome,
            FailureCode = failureCode,
            AfterJson = metadata == null ? null : JsonSerializer.Serialize(metadata, JsonOptions)
        });
    }
}
