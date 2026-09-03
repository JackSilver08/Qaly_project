using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Services.Privacy;

public sealed partial class PrivacyWorkProcessor
{
    private async Task ProcessDataSubjectRequestAsync(
        PrivacyWorkLease lease,
        string workerId,
        CancellationToken ct)
    {
        var request = await _db.DataSubjectRequests.FirstOrDefaultAsync(item => item.Id == lease.WorkId, ct);
        if (request == null ||
            request.Status != DataSubjectRequestStatuses.Collecting ||
            request.LeaseOwner != workerId ||
            request.LeaseExpiresAt is not { } leaseExpiresAt ||
            leaseExpiresAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        if (!request.TenantId.HasValue || !request.SubjectUserId.HasValue)
        {
            throw new InvalidOperationException("The data-subject request has no tenant or subject identity.");
        }

        if (request.RequestType == DataSubjectRequestTypes.Export)
        {
            await BuildExportAsync(request, lease, workerId, ct);
            return;
        }

        if (request.RequestType == DataSubjectRequestTypes.Delete)
        {
            await ExecuteDeletionAsync(request, lease, workerId, ct);
            return;
        }

        throw new InvalidOperationException($"Unsupported data-subject request type '{request.RequestType}'.");
    }

    private async Task BuildExportAsync(
        DataSubjectRequest request,
        PrivacyWorkLease lease,
        string workerId,
        CancellationToken ct)
    {
        var tenantId = request.TenantId!.Value;
        var subjectUserId = request.SubjectUserId!.Value;
        var scope = ParseScope(request.ScopeJson);
        var projectIds = await GetScopedProjectIdsAsync(tenantId, request.ProjectId, ct);
        var groupIds = await GetScopedGroupIdsAsync(tenantId, projectIds, ct);

        var user = await _db.Users.AsNoTracking()
            .Where(item => item.Id == subjectUserId)
            .Select(item => new
            {
                item.Id,
                item.FullName,
                item.Email,
                item.Role,
                item.IsActive,
                item.AvatarUrl,
                item.CreatedAt,
                item.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);
        var consents = await _db.PrivacyConsents.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == subjectUserId &&
                (!request.ProjectId.HasValue || item.ProjectId == request.ProjectId))
            .Select(item => new
            {
                item.Id,
                item.ProjectId,
                item.ConsentType,
                item.Purpose,
                item.SourceType,
                item.SourceEntityId,
                item.ProviderClass,
                item.PolicyVersion,
                item.NoticeVersion,
                item.Status,
                item.GrantedAt,
                item.RevokedAt,
                item.ExpiresAt
            })
            .ToListAsync(ct);

        var meetings = scope is "all" or "project" or "meetings"
            ? await _db.MeetingImports.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ImportedById == subjectUserId && projectIds.Contains(item.ProjectId))
                .Select(item => new
                {
                    item.Id,
                    item.ProjectId,
                    item.SourceProvider,
                    item.SourceId,
                    item.Title,
                    item.MeetingStartedAt,
                    item.Summary,
                    item.TranscriptText,
                    item.ParticipantsJson,
                    item.DataClassification,
                    item.PrivacyState,
                    item.ProcessingPurpose,
                    item.ProviderClass,
                    item.PolicyVersion,
                    item.RetentionExpiresAt,
                    item.CreatedAt
                })
                .ToListAsync(ct)
            : [];
        var aiJobs = scope is "all" or "project" or "ai" or "meetings"
            ? await _db.AiJobs.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.RequestedById == subjectUserId &&
                    (!item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value)))
                .Select(item => new
                {
                    item.Id,
                    item.ProjectId,
                    item.JobType,
                    item.SourceType,
                    item.SourceId,
                    item.SchemaId,
                    item.SchemaVersion,
                    item.Sensitive,
                    item.ConsentId,
                    item.RetentionPolicyId,
                    item.Status,
                    item.RequestJson,
                    item.ResultJson,
                    item.SelectedProvider,
                    item.SelectedModel,
                    item.CreatedAt,
                    item.FinishedAt
                })
                .ToListAsync(ct)
            : [];
        var drafts = scope is "all" or "project" or "ai" or "meetings"
            ? await _db.AiGeneratedDrafts.AsNoTracking()
                .Where(item => aiJobs.Select(job => job.Id).Contains(item.AiJobId))
                .Select(item => new
                {
                    item.Id,
                    item.AiJobId,
                    item.ProjectId,
                    item.DraftType,
                    item.Status,
                    item.OriginalPayloadJson,
                    item.WorkingPayloadJson,
                    item.ConfirmationResultJson,
                    item.CreatedAt,
                    item.ExpiresAt
                })
                .ToListAsync(ct)
            : [];
        var messages = scope is "all" or "project"
            ? await _db.GroupMessages.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.UserId == subjectUserId && groupIds.Contains(item.WorkGroupId))
                .Select(item => new
                {
                    item.Id,
                    item.WorkGroupId,
                    item.Content,
                    item.MessageType,
                    item.CreatedAt,
                    item.EditedAt,
                    item.IsDeleted,
                    item.DeletedAt
                })
                .ToListAsync(ct)
            : [];
        var comments = scope is "all" or "project"
            ? await _db.TaskComments.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.AuthorId == subjectUserId && projectIds.Contains(item.TaskItem.ProjectId))
                .Select(item => new
                {
                    item.Id,
                    item.TaskItemId,
                    item.Content,
                    item.CreatedAt,
                    item.IsDeleted,
                    item.DeletedAt
                })
                .ToListAsync(ct)
            : [];
        var tasks = scope is "all" or "project"
            ? await _db.TaskItems.IgnoreQueryFilters().AsNoTracking()
                .Where(item => projectIds.Contains(item.ProjectId) &&
                    (item.ReporterId == subjectUserId || item.AssigneeId == subjectUserId))
                .Select(item => new
                {
                    item.Id,
                    item.ProjectId,
                    item.Title,
                    item.Description,
                    item.Status,
                    item.Priority,
                    item.ReporterId,
                    item.AssigneeId,
                    item.CreatedAt,
                    item.UpdatedAt,
                    item.IsDeleted
                })
                .ToListAsync(ct)
            : [];

        var generatedAt = DateTimeOffset.UtcNow;
        var export = new
        {
            schema = "qaly.dsar.export.v1",
            generatedAt,
            request = new
            {
                request.Id,
                request.TenantId,
                request.ProjectId,
                request.SubjectUserId,
                request.RequestType,
                scope,
                request.RequestedAt,
                request.IdentityVerifiedAt,
                request.AcceptedAt
            },
            humanReadable = new
            {
                title = "Qaly data-subject export",
                summary = $"Export for subject {subjectUserId:D} in tenant {tenantId:D}.",
                counts = new
                {
                    consents = consents.Count,
                    meetings = meetings.Count,
                    aiJobs = aiJobs.Count,
                    drafts = drafts.Count,
                    messages = messages.Count,
                    comments = comments.Count,
                    tasks = tasks.Count
                }
            },
            machineReadable = new
            {
                user,
                consents,
                meetings,
                aiJobs,
                drafts,
                messages,
                comments,
                tasks
            }
        };
        var payload = JsonSerializer.Serialize(export, JsonOptions);
        request.EncryptedResultPayload = _payloadProtector.Protect(payload);
        request.ResultContentType = "application/json";
        request.ResultFileName = $"qaly-dsar-{request.Id:N}.json";
        request.ResultExpiresAt = generatedAt.AddMinutes(Math.Max(5, _options.ExportLifetimeMinutes));
        request.ResultSummaryJson = JsonSerializer.Serialize(new
        {
            consents = consents.Count,
            meetings = meetings.Count,
            aiJobs = aiJobs.Count,
            drafts = drafts.Count,
            messages = messages.Count,
            comments = comments.Count,
            tasks = tasks.Count
        }, JsonOptions);
        CompleteDataSubjectRequest(request, DataSubjectRequestStatuses.Completed, generatedAt);
        AddAudit(
            tenantId,
            request.ProjectId,
            null,
            "DSAR_EXPORT_COMPLETED",
            nameof(DataSubjectRequest),
            request.Id,
            null,
            null,
            PrivacyPurposes.SubjectExport,
            "completed",
            metadata: new Dictionary<string, string?>
            {
                ["scope"] = scope,
                ["artifactExpiresAt"] = request.ResultExpiresAt?.ToString("O")
            },
            dsarId: request.Id);
        await SaveIfLeaseOwnedAsync(lease, workerId, ct);
    }

    private async Task ExecuteDeletionAsync(
        DataSubjectRequest request,
        PrivacyWorkLease lease,
        string workerId,
        CancellationToken ct)
    {
        var tenantId = request.TenantId!.Value;
        var subjectUserId = request.SubjectUserId!.Value;
        var scope = ParseScope(request.ScopeJson);
        var projectIds = await GetScopedProjectIdsAsync(tenantId, request.ProjectId, ct);
        var groupIds = await GetScopedGroupIdsAsync(tenantId, projectIds, ct);
        var now = DateTimeOffset.UtcNow;

        var subjectHold = await _db.PrivacyLegalHolds.AsNoTracking().AnyAsync(hold =>
            hold.TenantId == tenantId &&
            hold.Status == PrivacyLegalHoldStatuses.Active &&
            hold.SubjectUserId == subjectUserId &&
            (!hold.ProjectId.HasValue || projectIds.Contains(hold.ProjectId.Value)), ct);
        if (subjectHold)
        {
            request.Status = DataSubjectRequestStatuses.ReviewRequired;
            request.LegalHoldDetected = true;
            request.LegalHoldReason = "An active subject-level legal hold requires operator review.";
            request.LeaseOwner = null;
            request.LeaseExpiresAt = null;
            AddAudit(
                tenantId,
                request.ProjectId,
                null,
                "DSAR_DELETE_BLOCKED_LEGAL_HOLD",
                nameof(DataSubjectRequest),
                request.Id,
                null,
                null,
                PrivacyPurposes.SubjectDeletion,
                "review_required",
                PrivacyErrorCodes.LegalHold,
                new Dictionary<string, string?> { ["scope"] = scope },
                request.Id);
            await SaveIfLeaseOwnedAsync(lease, workerId, ct);
            return;
        }

        foreach (var projectId in projectIds)
        {
            await InvalidateVectorAsync(projectId, subjectUserId, ct);
        }

        var heldMeetingIds = await _db.PrivacyLegalHolds.AsNoTracking()
            .Where(hold => hold.TenantId == tenantId &&
                hold.Status == PrivacyLegalHoldStatuses.Active &&
                hold.EntityType == nameof(MeetingImport) &&
                hold.EntityId.HasValue)
            .Select(hold => hold.EntityId!.Value)
            .ToHashSetAsync(ct);
        var deletedMeetingCount = 0;
        if (scope is "all" or "project" or "meetings")
        {
            var meetings = await _db.MeetingImports
                .Where(item => item.TenantId == tenantId && item.ImportedById == subjectUserId && projectIds.Contains(item.ProjectId))
                .ToListAsync(ct);
            foreach (var meeting in meetings.Where(item => !heldMeetingIds.Contains(item.Id)))
            {
                await EraseMeetingAndDerivedContentAsync(meeting, markDeleted: true, ct);
                deletedMeetingCount++;
            }
        }

        var redactedJobCount = 0;
        if (scope is "all" or "project" or "ai" or "meetings")
        {
            var jobs = await _db.AiJobs
                .Include(item => item.Drafts)
                .Where(item => item.TenantId == tenantId && item.RequestedById == subjectUserId &&
                    (!item.ProjectId.HasValue || projectIds.Contains(item.ProjectId.Value)))
                .ToListAsync(ct);
            foreach (var job in jobs)
            {
                WipeAiJob(job, now);
                redactedJobCount++;
            }

            var caches = await _db.AiPromptCache.Where(cache =>
                cache.TenantId == tenantId &&
                (!cache.ProjectId.HasValue || projectIds.Contains(cache.ProjectId.Value))).ToListAsync(ct);
            _db.AiPromptCache.RemoveRange(caches);
        }

        var anonymizedMessageCount = 0;
        var anonymizedCommentCount = 0;
        var retainedSharedTaskCount = 0;
        if (scope is "all" or "project")
        {
            var messages = await _db.GroupMessages.IgnoreQueryFilters()
                .Where(item => item.UserId == subjectUserId && groupIds.Contains(item.WorkGroupId))
                .ToListAsync(ct);
            foreach (var message in messages)
            {
                message.Content = "[removed by privacy request]";
                message.ReactionSummaryJson = "[]";
                anonymizedMessageCount++;
            }

            var comments = await _db.TaskComments.IgnoreQueryFilters()
                .Where(item => item.AuthorId == subjectUserId && projectIds.Contains(item.TaskItem.ProjectId))
                .ToListAsync(ct);
            foreach (var comment in comments)
            {
                comment.Content = "[removed by privacy request]";
                anonymizedCommentCount++;
            }

            retainedSharedTaskCount = await _db.TaskItems.IgnoreQueryFilters().CountAsync(item =>
                projectIds.Contains(item.ProjectId) &&
                (item.ReporterId == subjectUserId || item.AssigneeId == subjectUserId), ct);

            var notifications = await _db.Notifications.Where(item => item.UserId == subjectUserId).ToListAsync(ct);
            var pushSubscriptions = await _db.PushSubscriptions.Where(item => item.UserId == subjectUserId).ToListAsync(ct);
            _db.Notifications.RemoveRange(notifications);
            _db.PushSubscriptions.RemoveRange(pushSubscriptions);

            var apiKeys = await _db.ApiKeys.Where(item => item.UserId == subjectUserId).ToListAsync(ct);
            foreach (var apiKey in apiKeys)
            {
                apiKey.IsRevoked = true;
                apiKey.Name = "Revoked by privacy request";
                apiKey.Scopes = "[]";
            }
        }

        var consents = await _db.PrivacyConsents.Where(item =>
            item.TenantId == tenantId && item.UserId == subjectUserId && item.Status == PrivacyConsentStatuses.Granted).ToListAsync(ct);
        foreach (var consent in consents)
        {
            consent.Status = PrivacyConsentStatuses.Revoked;
            consent.RevokedAt = now;
        }

        var externalAffiliation = scope == "all" && await HasExternalTenantAffiliationAsync(subjectUserId, tenantId, ct);
        if (scope == "all" && !externalAffiliation)
        {
            var user = await _db.Users.FirstOrDefaultAsync(item => item.Id == subjectUserId, ct);
            if (user != null)
            {
                user.FullName = "Deleted user";
                user.Email = $"deleted-{subjectUserId:N}@invalid.local";
                user.PasswordHash = "!privacy-deleted";
                user.AvatarUrl = null;
                user.IsActive = false;
            }
        }

        var backupExpiryPending = true;
        var partial = backupExpiryPending || externalAffiliation || heldMeetingIds.Count > 0 || retainedSharedTaskCount > 0;
        request.LegalHoldDetected = heldMeetingIds.Count > 0;
        request.LegalHoldReason = heldMeetingIds.Count > 0
            ? "Some entity-scoped records remain under legal hold."
            : null;
        request.ResultSummaryJson = JsonSerializer.Serialize(new
        {
            scope,
            deletedMeetingCount,
            redactedJobCount,
            anonymizedMessageCount,
            anonymizedCommentCount,
            retainedSharedTaskCount,
            heldEntityCount = heldMeetingIds.Count,
            externalAffiliation,
            backupExpiryPending
        }, JsonOptions);
        CompleteDataSubjectRequest(
            request,
            partial ? DataSubjectRequestStatuses.PartiallyCompleted : DataSubjectRequestStatuses.Completed,
            now);
        AddAudit(
            tenantId,
            request.ProjectId,
            null,
            "DSAR_DELETE_COMPLETED",
            nameof(DataSubjectRequest),
            request.Id,
            null,
            null,
            PrivacyPurposes.SubjectDeletion,
            partial ? "partially_completed" : "completed",
            metadata: new Dictionary<string, string?>
            {
                ["scope"] = scope,
                ["heldEntityCount"] = heldMeetingIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["backupExpiryPending"] = backupExpiryPending.ToString()
            },
            dsarId: request.Id);
        await SaveIfLeaseOwnedAsync(lease, workerId, ct);
    }

    private async Task<List<Guid>> GetScopedProjectIdsAsync(Guid tenantId, Guid? projectId, CancellationToken ct)
    {
        var query = _db.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(project => (project.OrganizationId ?? project.Id) == tenantId);
        if (projectId.HasValue)
        {
            query = query.Where(project => project.Id == projectId.Value);
        }

        return await query.Select(project => project.Id).ToListAsync(ct);
    }

    private async Task<List<Guid>> GetScopedGroupIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> projectIds, CancellationToken ct)
    {
        var projectGroupIds = await _db.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(project => projectIds.Contains(project.Id) && project.SourceGroupId.HasValue)
            .Select(project => project.SourceGroupId!.Value)
            .ToListAsync(ct);
        return await _db.WorkGroups.IgnoreQueryFilters().AsNoTracking()
            .Where(group => group.OrganizationId == tenantId || projectGroupIds.Contains(group.Id))
            .Select(group => group.Id)
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task<bool> HasExternalTenantAffiliationAsync(Guid subjectUserId, Guid tenantId, CancellationToken ct)
        => await _db.OrganizationMembers.AnyAsync(member => member.UserId == subjectUserId && member.OrganizationId != tenantId, ct) ||
            await _db.Organizations.AnyAsync(organization => organization.OwnerId == subjectUserId && organization.Id != tenantId, ct) ||
            await _db.ProjectMembers.AnyAsync(member => member.UserId == subjectUserId &&
                (member.Project.OrganizationId ?? member.ProjectId) != tenantId, ct) ||
            await _db.Projects.AnyAsync(project => project.OwnerId == subjectUserId &&
                (project.OrganizationId ?? project.Id) != tenantId, ct);

    private static void CompleteDataSubjectRequest(
        DataSubjectRequest request,
        string status,
        DateTimeOffset completedAt)
    {
        request.Status = status;
        request.CompletedAt = completedAt;
        request.LeaseOwner = null;
        request.LeaseExpiresAt = null;
        request.LastErrorCode = null;
        request.LastErrorMessage = null;
    }

    private static string ParseScope(string scopeJson)
    {
        try
        {
            using var document = JsonDocument.Parse(scopeJson);
            return document.RootElement.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.String
                ? scope.GetString() ?? "all"
                : "all";
        }
        catch (JsonException)
        {
            return "all";
        }
    }
}
