using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.DTOs.Task;
using Qaly.Application.Services;
using Qaly.Application.Services.Groups;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed partial class AiNativeActionService : IAiNativeActionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ValidTaskPriorities =
        new HashSet<string>(["Low", "Medium", "High", "Critical"], StringComparer.OrdinalIgnoreCase);
    private readonly QalyDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiNativeAuthorizationService _authorization;
    private readonly IMemberSkillEvidenceService _skillEvidence;

    public AiNativeActionService(
        QalyDbContext db,
        ICurrentUserService currentUser,
        IAiNativeAuthorizationService authorization,
        IMemberSkillEvidenceService skillEvidence)
    {
        _db = db;
        _currentUser = currentUser;
        _authorization = authorization;
        _skillEvidence = skillEvidence;
    }

    public async Task<Result<AiNativeActionDraftDto>> PrepareAsync(
        string capabilityId,
        AiAssistantTurnRequestDto request,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId)
            return Result.Forbidden<AiNativeActionDraftDto>();
        if (!AiNativeDomainActionContract.CapabilityIds.Contains(capabilityId))
            return Result.Failure<AiNativeActionDraftDto>("Capability is not a registered native action.", 400, "assistant_capability_unknown");
        if (await _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct) != AiNativeSystemTier.Full)
            return Result.Forbidden<AiNativeActionDraftDto>("AI mutation drafts require Full AI access.");

        var prepared = capabilityId switch
        {
            AiNativeDomainActionContract.ChecklistCapability => await PrepareChecklistAsync(request, userId, ct),
            AiNativeDomainActionContract.BreakdownCapability => await PrepareBreakdownAsync(request, userId, ct),
            AiNativeDomainActionContract.WikiCapability => await PrepareWikiAsync(request, userId, ct),
            AiNativeDomainActionContract.GroupPollCapability => await PrepareGroupPollAsync(request, userId, ct),
            AiNativeDomainActionContract.DigestCapability => await PrepareDigestAsync(request, userId, ct),
            AiNativeDomainActionContract.MeetingActionsCapability => await PrepareMeetingActionsAsync(request, userId, ct),
            AiNativeDomainActionContract.RoadmapAdjustCapability => await PrepareRoadmapAdjustmentAsync(request, userId, ct),
            AiNativeDomainActionContract.SkillEvidenceCapability => await PrepareSkillEvidenceAsync(request, userId, ct),
            _ => null
        };
        if (prepared == null)
            return Result.Failure<AiNativeActionDraftDto>("A canonical target is required for this action.", 400, "assistant_target_required");
        if (!prepared.Authorized)
            return Result.Forbidden<AiNativeActionDraftDto>(prepared.Error ?? "You cannot draft this action in the current scope.");

        var draft = new AiNativeActionDraft
        {
            UserId = userId,
            ProjectId = prepared.ProjectId,
            OrganizationId = prepared.OrganizationId,
            CapabilityId = capabilityId,
            SchemaId = prepared.SchemaId,
            RendererId = AiNativeDomainActionContract.RendererId,
            TargetType = prepared.TargetType,
            TargetId = prepared.TargetId,
            PayloadJson = prepared.PayloadJson,
            SourceVersion = prepared.SourceVersion,
            Status = "pending_review",
            Revision = 1,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };
        _db.AiNativeActionDrafts.Add(draft);
        _db.AiAuditEvents.Add(Audit(userId, draft.ProjectId, "ai_native_action.drafted", draft, "pending_review"));
        await _db.SaveChangesAsync(ct);
        return Result.Success(ToDto(draft));
    }

    public async Task<Result<AiNativeActionDraftDto>> GetAsync(Guid draftId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiNativeActionDraftDto>();
        var draft = await _db.AiNativeActionDrafts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == draftId && item.UserId == userId, ct);
        if (draft == null) return Result.NotFound<AiNativeActionDraftDto>();
        if (await _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct) != AiNativeSystemTier.Full)
            return Result.Forbidden<AiNativeActionDraftDto>();
        var currentSource = await ResolveCurrentSourceVersionAsync(draft, userId, ct);
        if (!currentSource.Authorized) return Result.Forbidden<AiNativeActionDraftDto>();
        if (draft.Status == "pending_review" &&
            !string.Equals(currentSource.Version, draft.SourceVersion, StringComparison.Ordinal))
            return Result.Failure<AiNativeActionDraftDto>(
                "The source changed. Regenerate and review the draft.", 409, "source_stale");
        return Result.Success(ToDto(draft));
    }

    public async Task<Result<AiNativeActionDraftDto>> UpdateAsync(
        Guid draftId,
        UpdateAiNativeActionRequestDto request,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiNativeActionDraftDto>();
        var draft = await _db.AiNativeActionDrafts.SingleOrDefaultAsync(item => item.Id == draftId && item.UserId == userId, ct);
        if (draft == null) return Result.NotFound<AiNativeActionDraftDto>();
        if (await _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct) != AiNativeSystemTier.Full)
            return Result.Forbidden<AiNativeActionDraftDto>();
        if (draft.Status != "pending_review")
            return Result.Failure<AiNativeActionDraftDto>("The draft is no longer editable.", 409, "draft_not_pending");
        if (request.ExpectedRevision != draft.Revision || request.RowVersion != RevisionToken(draft))
            return Result.Failure<AiNativeActionDraftDto>("The draft changed. Reload before editing.", 409, "draft_stale");
        var currentSource = await ResolveCurrentSourceVersionAsync(draft, userId, ct);
        if (!currentSource.Authorized) return Result.Forbidden<AiNativeActionDraftDto>();
        if (!string.Equals(currentSource.Version, draft.SourceVersion, StringComparison.Ordinal))
            return Result.Failure<AiNativeActionDraftDto>(
                "The source changed. Regenerate and review the draft.", 409, "source_stale");
        var payloadJson = request.Payload.GetRawText();
        var validation = ValidatePayload(draft.CapabilityId, payloadJson, draft.TargetId);
        if (!validation.IsValid)
            return Result.Failure<AiNativeActionDraftDto>(validation.Error!, 400, "draft_payload_invalid");
        draft.PayloadJson = payloadJson;
        draft.Revision++;
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AiAuditEvents.Add(Audit(userId, draft.ProjectId, "ai_native_action.updated", draft, "pending_review"));
        await PersistDraftInAssistantTurnAsync(draft, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AiNativeActionDraftDto>("The draft changed. Reload before editing.", 409, "draft_stale");
        }
        return Result.Success(ToDto(draft));
    }

    public async Task<Result<AiNativeActionDraftDto>> RejectAsync(
        Guid draftId,
        RejectAiNativeActionRequestDto request,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiNativeActionDraftDto>();
        var draft = await _db.AiNativeActionDrafts.SingleOrDefaultAsync(item => item.Id == draftId && item.UserId == userId, ct);
        if (draft == null) return Result.NotFound<AiNativeActionDraftDto>();
        if (await _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct) != AiNativeSystemTier.Full)
            return Result.Forbidden<AiNativeActionDraftDto>();
        var currentSource = await ResolveCurrentSourceVersionAsync(draft, userId, ct);
        if (!currentSource.Authorized) return Result.Forbidden<AiNativeActionDraftDto>();
        if (draft.Status == "rejected") return Result.Success(ToDto(draft));
        if (draft.Status != "pending_review")
            return Result.Failure<AiNativeActionDraftDto>("The draft is no longer rejectable.", 409, "draft_not_pending");
        if (request.ExpectedRevision != draft.Revision || request.RowVersion != RevisionToken(draft))
            return Result.Failure<AiNativeActionDraftDto>("The draft changed. Reload before rejecting.", 409, "draft_stale");
        draft.Status = "rejected";
        draft.Revision++;
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AiAuditEvents.Add(Audit(userId, draft.ProjectId, "ai_native_action.rejected", draft, "rejected"));
        await PersistDraftInAssistantTurnAsync(draft, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AiNativeActionDraftDto>("The draft changed. Reload before rejecting.", 409, "draft_stale");
        }
        return Result.Success(ToDto(draft));
    }

    public async Task<Result<AiNativeActionReceiptDto>> ConfirmAsync(
        Guid draftId,
        ConfirmAiNativeActionRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<AiNativeActionReceiptDto>();
        idempotencyKey = idempotencyKey.Trim();
        if (idempotencyKey.Length is < 8 or > 180)
            return Result.Failure<AiNativeActionReceiptDto>("A stable Idempotency-Key is required.", 400, "idempotency_key_required");

        try
        {
            if (!_db.Database.IsRelational())
                return await ConfirmAttemptAsync(draftId, request, idempotencyKey, userId, ct);

            // SQL Server is configured with a retrying execution strategy. EF
            // requires the complete user transaction to run inside that strategy;
            // otherwise production confirmations fail before the first query.
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(
                () => ConfirmAttemptAsync(draftId, request, idempotencyKey, userId, ct));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AiNativeActionReceiptDto>("A source row changed during confirmation.", 409, "source_stale");
        }
    }

    private async Task<Result<AiNativeActionReceiptDto>> ConfirmAttemptAsync(
        Guid draftId,
        ConfirmAiNativeActionRequestDto request,
        string idempotencyKey,
        Guid userId,
        CancellationToken ct)
    {
        // A retry must start from canonical state, not entities left tracked by a
        // prior transient attempt. A committed first attempt will therefore replay
        // the stored receipt instead of duplicating the mutation.
        _db.ChangeTracker.Clear();

        var draft = await _db.AiNativeActionDrafts.SingleOrDefaultAsync(item => item.Id == draftId, ct);
        if (draft == null || draft.UserId != userId) return Result.NotFound<AiNativeActionReceiptDto>();
        if (await _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct) != AiNativeSystemTier.Full)
            return Result.Forbidden<AiNativeActionReceiptDto>();
        var currentSource = await ResolveCurrentSourceVersionAsync(draft, userId, ct);
        if (!currentSource.Authorized)
            return Result.Forbidden<AiNativeActionReceiptDto>(currentSource.Error ?? "Permission changed before confirmation.");
        if (draft.Status == "confirmed" && !string.IsNullOrWhiteSpace(draft.ReceiptJson))
        {
            if (!string.Equals(draft.ConfirmationIdempotencyKey, idempotencyKey, StringComparison.Ordinal))
                return Result.Failure<AiNativeActionReceiptDto>(
                    "This draft was confirmed with a different logical idempotency key.", 409, "idempotency_key_conflict");
            var replay = JsonSerializer.Deserialize<AiNativeActionReceiptDto>(draft.ReceiptJson, JsonOptions);
            return replay == null
                ? Result.Failure<AiNativeActionReceiptDto>("The stored receipt is invalid.", 500, "receipt_invalid")
                : Result.Success(replay with { Replayed = true });
        }
        if (draft.Status != "pending_review")
            return Result.Failure<AiNativeActionReceiptDto>("The draft is no longer confirmable.", 409, "draft_not_pending");
        if (draft.ExpiresAt <= DateTimeOffset.UtcNow)
            return Result.Failure<AiNativeActionReceiptDto>("The draft expired and must be regenerated.", 409, "draft_expired");
        if (request.ExpectedRevision != draft.Revision || request.RowVersion != RevisionToken(draft))
            return Result.Failure<AiNativeActionReceiptDto>("The draft changed. Reload before confirming.", 409, "draft_stale");
        if (!string.Equals(currentSource.Version, draft.SourceVersion, StringComparison.Ordinal))
            return Result.Failure<AiNativeActionReceiptDto>("The source changed. Regenerate and review the draft.", 409, "source_stale");

        var payloadJson = request.Payload?.GetRawText() ?? draft.PayloadJson;
        var validation = ValidatePayload(draft.CapabilityId, payloadJson, draft.TargetId);
        if (!validation.IsValid)
            return Result.Failure<AiNativeActionReceiptDto>(validation.Error!, 400, "draft_payload_invalid");

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var items = await ExecuteAsync(draft, payloadJson, userId, ct);
            if (!items.IsSuccess || items.Data == null)
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                return Result.Failure<AiNativeActionReceiptDto>(items.Error ?? "Native action failed.", items.StatusCode, items.ErrorCode);
            }

            // A successful SaveChanges is not enough for an AI receipt. Read every
            // canonical row back through no-tracking queries before we claim that
            // the action completed. Keeping this inside the transaction prevents a
            // false-success receipt if a write was filtered, rolled back, or mapped
            // to the wrong aggregate.
            var verifiedItems = await VerifyCanonicalReadBackAsync(items.Data, ct);
            if (!verifiedItems.IsSuccess || verifiedItems.Data == null)
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                return Result.Failure<AiNativeActionReceiptDto>(
                    verifiedItems.Error ?? "Canonical read-back failed.",
                    verifiedItems.StatusCode,
                    verifiedItems.ErrorCode);
            }

            var now = DateTimeOffset.UtcNow;
            var receipt = new AiNativeActionReceiptDto(
                AiNativeDomainActionContract.ReceiptSchemaId,
                Guid.NewGuid(),
                draft.Id,
                draft.CapabilityId,
                "confirmed",
                verifiedItems.Data,
                verifiedItems.Data.Select(item => item.Url).Distinct(StringComparer.Ordinal).ToArray(),
                now);
            draft.PayloadJson = payloadJson;
            draft.Status = "confirmed";
            draft.ConfirmationIdempotencyKey = idempotencyKey;
            draft.ReceiptJson = JsonSerializer.Serialize(receipt, JsonOptions);
            draft.ConfirmedAt = now;
            draft.Revision++;
            draft.UpdatedAt = now;
            _db.AiAuditEvents.Add(Audit(userId, draft.ProjectId, "ai_native_action.confirmed", draft, "confirmed", draft.ReceiptJson));
            await PersistDraftInAssistantTurnAsync(draft, ct);
            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            return Result.Success(receipt);
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<IReadOnlyList<TaskAcceptanceChecklistItemDto>>> GetChecklistAsync(
        Guid taskId,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<IReadOnlyList<TaskAcceptanceChecklistItemDto>>();
        if (await _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct) == AiNativeSystemTier.Restricted)
            return Result.Forbidden<IReadOnlyList<TaskAcceptanceChecklistItemDto>>();
        var task = await _db.TaskItems.AsNoTracking().Include(item => item.Project).ThenInclude(p => p.Members)
            .Include(item => item.Project).ThenInclude(p => p.Organization).ThenInclude(o => o!.Members)
            .SingleOrDefaultAsync(item => item.Id == taskId, ct);
        if (task == null) return Result.NotFound<IReadOnlyList<TaskAcceptanceChecklistItemDto>>();
        var permission = await ResolveProjectPermissionAsync(task.Project, userId, ct);
        if (!permission.CanRead) return Result.NotFound<IReadOnlyList<TaskAcceptanceChecklistItemDto>>();
        var entities = await _db.TaskAcceptanceChecklistItems.AsNoTracking().Where(item => item.TaskId == taskId)
            .OrderBy(item => item.SortOrder).ToListAsync(ct);
        var rows = entities.Select(ToChecklistDto).ToArray();
        return Result.Success<IReadOnlyList<TaskAcceptanceChecklistItemDto>>(rows);
    }

    public async Task<Result<TaskAcceptanceChecklistItemDto>> UpdateChecklistItemAsync(
        Guid itemId,
        UpdateTaskAcceptanceChecklistItemRequestDto request,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<TaskAcceptanceChecklistItemDto>();
        if (await _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct) != AiNativeSystemTier.Full)
            return Result.Forbidden<TaskAcceptanceChecklistItemDto>();
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            return Result.Failure<TaskAcceptanceChecklistItemDto>(
                "A current checklist rowVersion is required.", 400, "row_version_required");

        var item = await _db.TaskAcceptanceChecklistItems
            .Include(value => value.Task).ThenInclude(task => task.Project).ThenInclude(project => project.Members)
            .Include(value => value.Task).ThenInclude(task => task.Project).ThenInclude(project => project.Organization)
                .ThenInclude(organization => organization!.Members)
            .SingleOrDefaultAsync(value => value.Id == itemId, ct);
        if (item == null) return Result.NotFound<TaskAcceptanceChecklistItemDto>();

        var permission = await ResolveProjectPermissionAsync(item.Task.Project, userId, ct);
        if (!permission.CanManage)
            return permission.CanRead
                ? Result.Forbidden<TaskAcceptanceChecklistItemDto>()
                : Result.NotFound<TaskAcceptanceChecklistItemDto>();
        if (!string.Equals(ChecklistRevisionToken(item), request.RowVersion, StringComparison.Ordinal))
            return Result.Failure<TaskAcceptanceChecklistItemDto>(
                "The checklist changed. Reload before updating it.", 409, "checklist_stale");

        var previous = item.IsCompleted;
        item.IsCompleted = request.IsCompleted;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AiAuditEvents.Add(new AiAuditEvent
        {
            ActorUserId = userId,
            ProjectId = item.Task.ProjectId,
            EventType = "task_acceptance_checklist.updated",
            EntityType = nameof(TaskAcceptanceChecklistItem),
            EntityGuid = item.Id,
            EntityKey = item.TaskId.ToString(),
            Purpose = "task_acceptance_checklist",
            PolicyVersion = "ai-native-action.v1",
            DataClassification = "internal",
            ProviderClass = "manual",
            Outcome = "updated",
            BeforeJson = JsonSerializer.Serialize(new { isCompleted = previous }, JsonOptions),
            AfterJson = JsonSerializer.Serialize(new { isCompleted = item.IsCompleted }, JsonOptions)
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<TaskAcceptanceChecklistItemDto>(
                "The checklist changed. Reload before updating it.", 409, "checklist_stale");
        }

        return Result.Success(ToChecklistDto(item));
    }

    public async Task<Result<ProjectDigestSubscriptionDto?>> GetDigestSubscriptionAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid userId) return Result.Forbidden<ProjectDigestSubscriptionDto?>();
        if (await _authorization.ResolveSystemTierAsync(userId, _currentUser.Role, ct) == AiNativeSystemTier.Restricted)
            return Result.Forbidden<ProjectDigestSubscriptionDto?>();
        var project = await LoadProjectAsync(projectId, ct);
        if (project == null) return Result.NotFound<ProjectDigestSubscriptionDto?>();
        var permission = await ResolveProjectPermissionAsync(project, userId, ct);
        if (!permission.CanRead) return Result.NotFound<ProjectDigestSubscriptionDto?>();
        var value = await _db.ProjectDigestSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.UserId == userId, ct);
        return Result.Success(value == null ? null : ToDto(value));
    }

    private async Task<PreparedDraft?> PrepareChecklistAsync(AiAssistantTurnRequestDto request, Guid userId, CancellationToken ct)
    {
        var task = await ResolveTaskAsync(request.Context, ct);
        if (task == null) return null;
        var permission = await ResolveProjectPermissionAsync(task.Project, userId, ct);
        var items = BuildChecklist(task, RequestedCount(request.Message, 5));
        var payload = new AiNativeChecklistPayloadDto(task.Id, task.Title, items, $"/projects/{task.ProjectId}/tasks/{task.Id}");
        return Prepared(permission.CanManage, permission.CanManage ? null : "Project Manager permission is required.",
            task.ProjectId, task.Project.OrganizationId, AiNativeDomainActionContract.ChecklistSchemaId,
            "task", task.Id, JsonSerializer.Serialize(payload, JsonOptions),
            TaskSourceVersion(task, AiNativeDomainActionContract.ChecklistCapability));
    }

    private async Task<PreparedDraft?> PrepareBreakdownAsync(AiAssistantTurnRequestDto request, Guid userId, CancellationToken ct)
    {
        var task = await ResolveTaskAsync(request.Context, ct);
        if (task == null) return null;
        var permission = await ResolveProjectPermissionAsync(task.Project, userId, ct);
        var count = RequestedCount(request.Message, 5);
        var requiredSkills = task.SkillRequirements
            .Where(item => item.OrganizationSkill.IsActive)
            .OrderBy(item => item.OrganizationSkill.Name)
            .Select(item => new RequiredSkillOption(item.OrganizationSkillId, item.OrganizationSkill.Name))
            .ToList();
        var availableSkills = requiredSkills.ToList();
        if (task.Project.OrganizationId.HasValue)
        {
            availableSkills = await _db.OrganizationSkills.AsNoTracking()
                .Where(item => item.OrganizationId == task.Project.OrganizationId.Value && item.IsActive)
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Id)
                .Select(item => new RequiredSkillOption(item.Id, item.Name))
                .ToListAsync(ct);
        }
        if (requiredSkills.Count == 0)
            requiredSkills = availableSkills.Take(Math.Max(1, count)).ToList();
        var subtasks = BuildSubtasks(task, request.Message, count, requiredSkills);
        var skillOptions = availableSkills
            .Select(item => new AiNativeBreakdownSkillOptionDto(item.SkillId, item.Name))
            .ToArray();
        var payload = new AiNativeBreakdownPayloadDto(
            task.Id,
            task.Title,
            subtasks,
            $"/projects/{task.ProjectId}/tasks/{task.Id}",
            skillOptions);
        return Prepared(permission.CanManage, permission.CanManage ? null : "Project Manager permission is required.",
            task.ProjectId, task.Project.OrganizationId, AiNativeDomainActionContract.BreakdownSchemaId,
            "task", task.Id, JsonSerializer.Serialize(payload, JsonOptions),
            TaskSourceVersion(task, AiNativeDomainActionContract.BreakdownCapability));
    }

    private async Task<PreparedDraft?> PrepareWikiAsync(AiAssistantTurnRequestDto request, Guid userId, CancellationToken ct)
    {
        var wikiId = string.Equals(request.Context?.EntityType, "wiki", StringComparison.OrdinalIgnoreCase)
            ? request.Context?.EntityId : null;
        if (!wikiId.HasValue) return null;
        var wiki = await _db.WikiPages.AsNoTracking().Include(item => item.Project).ThenInclude(p => p.Members)
            .Include(item => item.Project).ThenInclude(p => p.Organization).ThenInclude(o => o!.Members)
            .SingleOrDefaultAsync(item => item.Id == wikiId.Value, ct);
        if (wiki == null) return null;
        var permission = await ResolveProjectPermissionAsync(wiki.Project, userId, ct);
        var summary = Summarize(wiki.Content);
        var sourceRef = $"/projects/{wiki.ProjectId}/wiki/{wiki.Id}";
        var sectionRefs = ExtractWikiSections(wiki.Content, sourceRef);
        var taskCandidates = BuildWikiTaskCandidates(wiki.Title, summary, sectionRefs);
        var payload = new AiNativeWikiPayloadDto(wiki.Id, wiki.ProjectId, wiki.Title, summary,
            taskCandidates[0].Title, taskCandidates[0].Description, false,
            sourceRef, sectionRefs, taskCandidates);
        return Prepared(permission.CanManage, permission.CanManage ? null : "Project Manager permission is required.",
            wiki.ProjectId, wiki.Project.OrganizationId, AiNativeDomainActionContract.WikiSchemaId,
            "wiki", wiki.Id, JsonSerializer.Serialize(payload, JsonOptions), WikiSourceVersion(wiki));
    }

    private async Task<PreparedDraft?> PrepareGroupPollAsync(AiAssistantTurnRequestDto request, Guid userId, CancellationToken ct)
    {
        var groupId = string.Equals(request.Context?.EntityType, "group", StringComparison.OrdinalIgnoreCase)
            ? request.Context?.EntityId : null;
        if (!groupId.HasValue) return null;
        var group = await _db.WorkGroups.AsNoTracking().Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == groupId.Value, ct);
        if (group == null) return null;
        var role = group.OwnerId == userId ? GroupRoleRules.Owner : group.Members.FirstOrDefault(item => item.UserId == userId)?.Role;
        var optionCount = RequestedOptionCount(request.Message, 3);
        var (question, options) = ParseGroupPoll(
            request.Message,
            $"Nhóm {group.Name} chọn phương án triển khai nào?",
            optionCount);
        var payload = new AiNativeGroupPollPayloadDto(group.Id, group.Name, question, options, false,
            RequestedExpiry(request.Message),
            $"/groups/{group.Id}");
        return Prepared(GroupRoleRules.CanCreatePoll(role), GroupRoleRules.CanCreatePoll(role) ? null : "Group Owner/Admin permission is required.",
            null, group.OrganizationId, AiNativeDomainActionContract.GroupPollSchemaId,
            "group", group.Id, JsonSerializer.Serialize(payload, JsonOptions), GroupSourceVersion(group));
    }

    private async Task<PreparedDraft?> PrepareDigestAsync(AiAssistantTurnRequestDto request, Guid userId, CancellationToken ct)
    {
        var projectId = request.Context?.ProjectId ??
            (string.Equals(request.Context?.EntityType, "project", StringComparison.OrdinalIgnoreCase) ? request.Context?.EntityId : null);
        if (!projectId.HasValue) return null;
        var project = await LoadProjectAsync(projectId.Value, ct);
        if (project == null) return null;
        var permission = await ResolveProjectPermissionAsync(project, userId, ct);
        var current = await _db.ProjectDigestSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectId == project.Id && item.UserId == userId, ct);
        var organizationTimeZone = project.OrganizationId.HasValue
            ? await _db.OrganizationMemberCapacityProfiles.AsNoTracking()
                .Where(item => item.OrganizationId == project.OrganizationId.Value && item.UserId == userId)
                .Select(item => item.TimeZoneId)
                .SingleOrDefaultAsync(ct)
            : null;
        var schedule = ParseDigestSchedule(
            request.Message,
            current?.DayOfWeek ?? 1,
            current?.LocalTimeMinutes ?? 540,
            current?.TimeZoneId ?? organizationTimeZone ?? "Asia/Ho_Chi_Minh");
        var payload = new AiNativeDigestPayloadDto(project.Id, project.Name,
            RequestedDigestEnabled(request.Message, current?.IsEnabled ?? true),
            schedule.DayOfWeek, schedule.LocalTimeMinutes, schedule.TimeZoneId,
            $"/projects/{project.Id}");
        return Prepared(permission.CanManage, permission.CanManage ? null : "Project Manager permission is required.",
            project.Id, project.OrganizationId, AiNativeDomainActionContract.DigestSchemaId,
            "project", project.Id, JsonSerializer.Serialize(payload, JsonOptions),
            DigestSourceVersion(project, current, organizationTimeZone));
    }

    private async Task<PreparedDraft?> PrepareMeetingActionsAsync(
        AiAssistantTurnRequestDto request,
        Guid userId,
        CancellationToken ct)
    {
        var meetingId = string.Equals(request.Context?.EntityType, "meeting", StringComparison.OrdinalIgnoreCase)
            ? request.Context?.EntityId
            : null;
        if (!meetingId.HasValue) return null;
        var meeting = await _db.MeetingImports.AsNoTracking()
            .Include(item => item.Project).ThenInclude(project => project.Members)
            .Include(item => item.Project).ThenInclude(project => project.Organization).ThenInclude(org => org!.Members)
            .Include(item => item.AiDraft)
            .Include(item => item.ActionItemMappings)
            .Where(item => item.Id == meetingId.Value || item.SourceId == meetingId.Value.ToString())
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (meeting == null) return null;
        var permission = await ResolveProjectPermissionAsync(meeting.Project, userId, ct);
        var extractionJson = string.IsNullOrWhiteSpace(meeting.AiDraft?.WorkingPayloadJson)
            ? meeting.AiDraft?.PayloadJson
            : meeting.AiDraft.WorkingPayloadJson;
        MeetingExtractionPayload? extraction = null;
        if (!string.IsNullOrWhiteSpace(extractionJson))
        {
            try { extraction = JsonSerializer.Deserialize<MeetingExtractionPayload>(extractionJson, JsonOptions); }
            catch (JsonException) { /* The persisted summary remains a useful truthful fallback. */ }
        }
        var actionItems = (extraction?.ActionItems ?? [])
            .Select((item, index) => new AiNativeMeetingActionItemDto(
                index,
                item.Title,
                item.Description,
                NormalizePriority(item.Priority),
                item.SourceEvidence,
                "none"))
            .ToArray();
        var taskOptions = await _db.TaskItems.AsNoTracking()
            .Where(item => item.ProjectId == meeting.ProjectId && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ThenBy(item => item.Id)
            .Take(50)
            .Select(item => new AiNativeMeetingTaskOptionDto(item.Id, item.Title))
            .ToListAsync(ct);
        var payload = new AiNativeMeetingActionsPayloadDto(
            meeting.Id,
            meeting.ProjectId,
            meeting.Title,
            extraction?.Meeting.Summary ?? meeting.Summary ?? Summarize(meeting.TranscriptText),
            (extraction?.Decisions ?? []).Select(item => item.Text).ToArray(),
            (extraction?.Risks ?? []).Select(item => item.Text).ToArray(),
            actionItems,
            taskOptions,
            $"/projects/{meeting.ProjectId}?tab=activity&meetingImportId={meeting.Id}");
        return Prepared(
            permission.CanManage,
            permission.CanManage ? null : "Project Manager permission is required.",
            meeting.ProjectId,
            meeting.Project.OrganizationId,
            AiNativeDomainActionContract.MeetingActionsSchemaId,
            "meeting",
            meeting.Id,
            JsonSerializer.Serialize(payload, JsonOptions),
            MeetingSourceVersion(meeting));
    }

    private async Task<PreparedDraft?> PrepareRoadmapAdjustmentAsync(
        AiAssistantTurnRequestDto request,
        Guid userId,
        CancellationToken ct)
    {
        var projectId = request.Context?.ProjectId ??
            (string.Equals(request.Context?.EntityType, "project", StringComparison.OrdinalIgnoreCase)
                ? request.Context?.EntityId
                : null);
        if (!projectId.HasValue) return null;
        var project = await LoadRoadmapProjectAsync(projectId.Value, ct);
        if (project == null) return null;
        var permission = await ResolveProjectPermissionAsync(project, userId, ct);
        var projectMemberIds = project.Members.Select(member => member.UserId).ToArray();
        var capacity = project.OrganizationId.HasValue
            ? await _db.OrganizationMemberCapacityProfiles.AsNoTracking()
                .Where(item => item.OrganizationId == project.OrganizationId.Value &&
                    projectMemberIds.Contains(item.UserId))
                .SumAsync(item => (decimal?)item.WeeklyCapacityHours, ct) ?? 0m
            : 0m;
        var adjustments = BuildRoadmapAdjustments(project, capacity);
        var payload = new AiNativeRoadmapAdjustmentPayloadDto(
            project.Id,
            project.Name,
            $"Đã đối chiếu {project.Sprints.Count} Sprint, {project.Tasks.Count} Task, dependency, deadline và {capacity:0.#} giờ capacity/tuần đã khai báo.",
            adjustments,
            $"/projects/{project.Id}?tab=roadmap");
        return Prepared(
            permission.CanManage,
            permission.CanManage ? null : "Project Manager permission is required.",
            project.Id,
            project.OrganizationId,
            AiNativeDomainActionContract.RoadmapAdjustSchemaId,
            "project",
            project.Id,
            JsonSerializer.Serialize(payload, JsonOptions),
            RoadmapSourceVersion(project, capacity));
    }

    private async Task<PreparedDraft?> PrepareSkillEvidenceAsync(
        AiAssistantTurnRequestDto request,
        Guid userId,
        CancellationToken ct)
    {
        var taskId = string.Equals(request.Context?.EntityType, "task", StringComparison.OrdinalIgnoreCase)
            ? request.Context?.EntityId
            : null;
        if (!taskId.HasValue) return null;
        var task = await LoadSkillEvidenceTaskAsync(taskId.Value, ct);
        if (task == null) return null;
        var permission = await ResolveProjectPermissionAsync(task.Project, userId, ct);
        if (!string.Equals(task.Status, "Done", StringComparison.OrdinalIgnoreCase))
            return Prepared(false, "Task must be Done before skill evidence can be reviewed.", task.ProjectId,
                task.Project.OrganizationId, AiNativeDomainActionContract.SkillEvidenceSchemaId, "task", task.Id,
                "{}", SkillEvidenceSourceVersion(task));
        if (task.AcceptanceChecklist.Count == 0 || task.AcceptanceChecklist.Any(item => !item.IsCompleted))
            return Prepared(false, "Confirmed acceptance checklist evidence is required.", task.ProjectId,
                task.Project.OrganizationId, AiNativeDomainActionContract.SkillEvidenceSchemaId, "task", task.Id,
                "{}", SkillEvidenceSourceVersion(task));
        var candidates = task.Assignees
            .Select(item => new AiNativeSkillEvidenceCandidateDto(item.UserId, item.User.FullName, true))
            .Concat(task.Assignee == null
                ? []
                : [new AiNativeSkillEvidenceCandidateDto(task.Assignee.Id, task.Assignee.FullName, true)])
            .GroupBy(item => item.UserId)
            .Select(group => group.First())
            .ToArray();
        var payload = new AiNativeSkillEvidencePayloadDto(
            task.Id,
            task.ProjectId,
            task.Title,
            Convert.ToBase64String(task.RowVersion),
            candidates,
            task.SkillRequirements.Select(item => new AiNativeSkillEvidenceSkillDto(
                item.OrganizationSkillId,
                item.OrganizationSkill.Name,
                item.RequiredLevel)).ToArray(),
            task.AcceptanceChecklist.OrderBy(item => item.SortOrder).Select(item => item.Text).ToArray(),
            $"/projects/{task.ProjectId}/tasks/{task.Id}");
        return Prepared(
            permission.CanManage && candidates.Length > 0 && payload.Skills.Count > 0,
            !permission.CanManage
                ? "Project Manager permission is required."
                : candidates.Length == 0
                    ? "A confirmed assignee is required before attribution."
                    : payload.Skills.Count == 0
                        ? "The Task has no confirmed required skill."
                        : null,
            task.ProjectId,
            task.Project.OrganizationId,
            AiNativeDomainActionContract.SkillEvidenceSchemaId,
            "task",
            task.Id,
            JsonSerializer.Serialize(payload, JsonOptions),
            SkillEvidenceSourceVersion(task));
    }

    private async Task<Result<IReadOnlyList<AiNativeActionReceiptItemDto>>> ExecuteAsync(
        AiNativeActionDraft draft, string payloadJson, Guid userId, CancellationToken ct)
    {
        var result = new List<AiNativeActionReceiptItemDto>();
        switch (draft.CapabilityId)
        {
            case AiNativeDomainActionContract.ChecklistCapability:
            {
                var payload = JsonSerializer.Deserialize<AiNativeChecklistPayloadDto>(payloadJson, JsonOptions)!;
                var existing = await _db.TaskAcceptanceChecklistItems.Where(item => item.TaskId == payload.TaskId).ToListAsync(ct);
                _db.TaskAcceptanceChecklistItems.RemoveRange(existing);
                var rows = payload.Items.Select((text, index) => new TaskAcceptanceChecklistItem
                {
                    TaskId = payload.TaskId, Text = text.Trim(), SortOrder = index, CreatedByUserId = userId,
                    SourceDraftId = draft.Id
                }).ToList();
                _db.TaskAcceptanceChecklistItems.AddRange(rows);
                await _db.SaveChangesAsync(ct);
                result.AddRange(rows.Select(row => new AiNativeActionReceiptItemDto(
                    "acceptance_checklist_item", row.Id, row.Text,
                    $"/projects/{draft.ProjectId}/tasks/{payload.TaskId}")));
                break;
            }
            case AiNativeDomainActionContract.BreakdownCapability:
            {
                var payload = JsonSerializer.Deserialize<AiNativeBreakdownPayloadDto>(payloadJson, JsonOptions)!;
                var requiredSkillIds = payload.Subtasks
                    .Where(item => item.RequiredSkillId.HasValue)
                    .Select(item => item.RequiredSkillId!.Value)
                    .Distinct()
                    .ToArray();
                var activeSkillIds = requiredSkillIds.Length == 0
                    ? []
                    : await _db.OrganizationSkills.AsNoTracking()
                        .Where(item => requiredSkillIds.Contains(item.Id) && item.IsActive &&
                            item.OrganizationId == draft.OrganizationId)
                        .Select(item => item.Id)
                        .ToArrayAsync(ct);
                if (activeSkillIds.Length != requiredSkillIds.Length)
                    return Result.Failure<IReadOnlyList<AiNativeActionReceiptItemDto>>(
                        "A reviewed required skill is missing, inactive, or outside this Organization. Regenerate the draft.",
                        409, "source_stale");
                var parent = await _db.TaskItems.SingleAsync(item => item.Id == payload.ParentTaskId, ct);
                // Once a parent is decomposed, its canonical progress is the aggregate of
                // the leaf subtasks. Keeping both parent and children progress-contributing
                // would double-count one unit of work throughout Dashboard/Analytics.
                parent.ContributesToProgress = false;
                var rows = payload.Subtasks.Select((item, index) => new TaskItem
                {
                    ProjectId = parent.ProjectId, SprintId = parent.SprintId, ParentTaskId = parent.Id,
                    ReporterId = userId, Title = item.Title.Trim(), Description = item.Description?.Trim(),
                    Priority = NormalizePriority(item.Priority), EstimatedHours = item.EstimatedHours,
                    DueDate = parent.DueDate, Status = "Todo", SortOrder = index
                }).ToList();
                _db.TaskItems.AddRange(rows);
                var skillRequirements = payload.Subtasks.Select((item, index) => new { item, row = rows[index] })
                    .Where(value => value.item.RequiredSkillId.HasValue)
                    .Select(value => new TaskSkillRequirement
                    {
                        TaskItemId = value.row.Id,
                        OrganizationSkillId = value.item.RequiredSkillId!.Value,
                        RequiredLevel = "Intermediate",
                        Provenance = "AI_REVIEWED",
                        ConfirmedByUserId = userId,
                        ConfirmedAt = DateTimeOffset.UtcNow
                    })
                    .ToList();
                _db.TaskSkillRequirements.AddRange(skillRequirements);
                for (var index = 1; index < rows.Count; index++)
                {
                    if (!payload.Subtasks[index].DependsOnPrevious) continue;
                    _db.TaskDependencies.Add(new TaskDependency
                    {
                        PredecessorId = rows[index - 1].Id,
                        SuccessorId = rows[index].Id,
                        DependencyType = "FinishToStart"
                    });
                }
                await _db.SaveChangesAsync(ct);
                result.AddRange(rows.Select(row => new AiNativeActionReceiptItemDto(
                    "task", row.Id, row.Title, $"/projects/{row.ProjectId}/tasks/{row.Id}")));
                break;
            }
            case AiNativeDomainActionContract.WikiCapability:
            {
                var payload = JsonSerializer.Deserialize<AiNativeWikiPayloadDto>(payloadJson, JsonOptions)!;
                var canonicalWiki = await _db.WikiPages.AsNoTracking()
                    .Where(item => item.Id == draft.TargetId)
                    .Select(item => new { item.Id, item.ProjectId, item.Title })
                    .SingleAsync(ct);
                var selectedCandidates = payload.TaskCandidates?.Where(item => item.Selected).ToArray();
                if (selectedCandidates?.Length > 0)
                {
                    var tasks = selectedCandidates.Select(candidate => new TaskItem
                    {
                        ProjectId = canonicalWiki.ProjectId,
                        ReporterId = userId,
                        Title = candidate.Title.Trim(),
                        Description = $"{candidate.Description.Trim()}\n\nNguồn: {candidate.SourceRef.Trim()}",
                        Priority = "Medium",
                        Status = "Todo"
                    }).ToList();
                    _db.TaskItems.AddRange(tasks);
                    await _db.SaveChangesAsync(ct);
                    result.AddRange(tasks.Select(task => new AiNativeActionReceiptItemDto(
                        "task", task.Id, task.Title, $"/projects/{task.ProjectId}/tasks/{task.Id}")));
                }
                else if (payload.TaskCandidates == null && payload.CreateTask)
                {
                    // Compatibility for drafts created before the multi-task Wiki contract.
                    var task = new TaskItem
                    {
                        ProjectId = canonicalWiki.ProjectId,
                        ReporterId = userId,
                        Title = payload.TaskTitle.Trim(),
                        Description = payload.TaskDescription.Trim(),
                        Priority = "Medium",
                        Status = "Todo"
                    };
                    _db.TaskItems.Add(task);
                    await _db.SaveChangesAsync(ct);
                    result.Add(new AiNativeActionReceiptItemDto(
                        "task", task.Id, task.Title, $"/projects/{task.ProjectId}/tasks/{task.Id}"));
                }
                result.Add(new AiNativeActionReceiptItemDto("wiki_brief", canonicalWiki.Id, canonicalWiki.Title,
                    $"/projects/{canonicalWiki.ProjectId}/wiki/{canonicalWiki.Id}"));
                break;
            }
            case AiNativeDomainActionContract.GroupPollCapability:
            {
                var payload = JsonSerializer.Deserialize<AiNativeGroupPollPayloadDto>(payloadJson, JsonOptions)!;
                var poll = new GroupPoll
                {
                    GroupId = payload.GroupId, CreatedByUserId = userId, Question = payload.Question.Trim(),
                    AllowMultiple = payload.AllowMultiple, ExpiredAt = payload.ExpiredAt
                };
                poll.Options = payload.Options.Select((text, index) => new GroupPollOption
                {
                    PollId = poll.Id, Content = text.Trim(), SortOrder = index
                }).ToList();
                var pollMessageContent = string.Join('\n', new[]
                {
                    $"[poll] {poll.Question}",
                    $"[pollid] {poll.Id}"
                }.Concat(poll.Options.OrderBy(item => item.SortOrder)
                    .Select((option, index) => $"{index + 1}. {option.Content}")));
                if (pollMessageContent.Length > 4000)
                    return Result.Failure<IReadOnlyList<AiNativeActionReceiptItemDto>>(
                        "Poll is too large to publish in group chat. Shorten the question or options.",
                        400,
                        "poll_message_too_large");
                _db.GroupPolls.Add(poll);
                _db.GroupMessages.Add(new GroupMessage
                {
                    WorkGroupId = payload.GroupId,
                    UserId = userId,
                    Content = pollMessageContent,
                    MessageType = "Poll"
                });
                // The Poll and its visible Group card are one canonical business
                // operation. The surrounding native-action transaction makes this
                // SaveChanges atomic with the draft receipt.
                await _db.SaveChangesAsync(ct);
                result.Add(new AiNativeActionReceiptItemDto("group_poll", poll.Id, poll.Question,
                    $"/groups/{poll.GroupId}/polls/{poll.Id}"));
                break;
            }
            case AiNativeDomainActionContract.DigestCapability:
            {
                var payload = JsonSerializer.Deserialize<AiNativeDigestPayloadDto>(payloadJson, JsonOptions)!;
                var subscription = await _db.ProjectDigestSubscriptions
                    .SingleOrDefaultAsync(item => item.ProjectId == payload.ProjectId && item.UserId == userId, ct);
                if (subscription == null)
                {
                    subscription = new ProjectDigestSubscription { UserId = userId, ProjectId = payload.ProjectId };
                    _db.ProjectDigestSubscriptions.Add(subscription);
                }
                subscription.IsEnabled = payload.IsEnabled;
                subscription.Cadence = "weekly";
                subscription.DayOfWeek = payload.DayOfWeek;
                subscription.LocalTimeMinutes = payload.LocalTimeMinutes;
                subscription.TimeZoneId = payload.TimeZoneId.Trim();
                subscription.NextDeliveryAt = payload.IsEnabled ? NextDelivery(payload) : null;
                subscription.LastDeliveryStatus = payload.IsEnabled ? "scheduled" : "disabled";
                subscription.LastError = null;
                subscription.LastDeliveryKey = null;
                subscription.LastAttemptAt = null;
                subscription.ConsecutiveFailureCount = 0;
                subscription.Revision++;
                await _db.SaveChangesAsync(ct);
                result.Add(new AiNativeActionReceiptItemDto("project_digest_subscription", subscription.Id,
                    payload.IsEnabled ? "Weekly digest enabled" : "Weekly digest disabled",
                    $"/projects/{payload.ProjectId}"));
                break;
            }
            case AiNativeDomainActionContract.MeetingActionsCapability:
            {
                var payload = JsonSerializer.Deserialize<AiNativeMeetingActionsPayloadDto>(payloadJson, JsonOptions)!;
                var selected = payload.ActionItems
                    .Where(item => item.MappingMode is "existing_task" or "new_task")
                    .ToArray();
                var existingMappings = await _db.MeetingActionItemMappings
                    .Where(item => item.MeetingImportId == payload.MeetingImportId)
                    .ToListAsync(ct);
                if (selected.Any(item => existingMappings.Any(mapping => mapping.ActionItemIndex == item.ItemIndex)))
                    return Result.Failure<IReadOnlyList<AiNativeActionReceiptItemDto>>(
                        "A reviewed meeting action was already mapped. Reload before confirming.", 409, "source_stale");
                var existingTaskIds = selected
                    .Where(item => item.MappingMode == "existing_task" && item.ExistingTaskId.HasValue)
                    .Select(item => item.ExistingTaskId!.Value)
                    .Distinct()
                    .ToArray();
                var validExistingTaskIds = existingTaskIds.Length == 0
                    ? []
                    : await _db.TaskItems.AsNoTracking()
                        .Where(item => existingTaskIds.Contains(item.Id) && item.ProjectId == payload.ProjectId && !item.IsDeleted)
                        .Select(item => item.Id)
                        .ToArrayAsync(ct);
                if (validExistingTaskIds.Length != existingTaskIds.Length)
                    return Result.Failure<IReadOnlyList<AiNativeActionReceiptItemDto>>(
                        "An existing Task selection is stale or outside the Meeting Project.", 409, "source_stale");

                foreach (var action in selected)
                {
                    Guid taskId;
                    string taskTitle;
                    if (action.MappingMode == "new_task")
                    {
                        var task = new TaskItem
                        {
                            ProjectId = payload.ProjectId,
                            ReporterId = userId,
                            Title = action.Title.Trim(),
                            Description = $"{action.Description?.Trim()}\n\nNguồn cuộc họp: {action.SourceEvidence?.Trim()}",
                            Priority = NormalizePriority(action.Priority),
                            Status = "Todo"
                        };
                        _db.TaskItems.Add(task);
                        taskId = task.Id;
                        taskTitle = task.Title;
                    }
                    else
                    {
                        taskId = action.ExistingTaskId!.Value;
                        taskTitle = payload.ExistingTaskOptions.First(item => item.TaskId == taskId).Title;
                    }
                    _db.MeetingActionItemMappings.Add(new MeetingActionItemMapping
                    {
                        MeetingImportId = payload.MeetingImportId,
                        ActionItemIndex = action.ItemIndex,
                        TaskId = taskId,
                        Status = "Linked",
                        SourceTitle = action.Title.Trim(),
                        SourcePriority = NormalizePriority(action.Priority),
                        SourceQuote = action.SourceEvidence ?? action.Description,
                        CreatedById = userId
                    });
                    result.Add(new AiNativeActionReceiptItemDto(
                        "meeting_action_mapping",
                        taskId,
                        taskTitle,
                        $"/projects/{payload.ProjectId}/tasks/{taskId}"));
                }
                await _db.SaveChangesAsync(ct);
                result.Add(new AiNativeActionReceiptItemDto(
                    "meeting_review", payload.MeetingImportId, payload.MeetingTitle, payload.SourceRef));
                break;
            }
            case AiNativeDomainActionContract.RoadmapAdjustCapability:
            {
                var payload = JsonSerializer.Deserialize<AiNativeRoadmapAdjustmentPayloadDto>(payloadJson, JsonOptions)!;
                foreach (var adjustment in payload.Adjustments.Where(item => item.Selected))
                {
                    Sprint sprint;
                    if (adjustment.SprintId.HasValue)
                    {
                        sprint = await _db.Set<Sprint>().SingleAsync(item =>
                            item.Id == adjustment.SprintId.Value && item.ProjectId == payload.ProjectId, ct);
                        sprint.Name = adjustment.SprintName.Trim();
                        sprint.StartDate = adjustment.AfterStart;
                        sprint.EndDate = adjustment.AfterEnd;
                        sprint.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        sprint = new Sprint
                        {
                            ProjectId = payload.ProjectId,
                            Name = adjustment.SprintName.Trim(),
                            Goal = adjustment.Reason.Trim(),
                            StartDate = adjustment.AfterStart,
                            EndDate = adjustment.AfterEnd,
                            Status = "Planning"
                        };
                        _db.Set<Sprint>().Add(sprint);
                    }
                    result.Add(new AiNativeActionReceiptItemDto(
                        "sprint", sprint.Id, sprint.Name, $"/projects/{payload.ProjectId}?tab=roadmap"));
                }
                await _db.SaveChangesAsync(ct);
                break;
            }
            case AiNativeDomainActionContract.SkillEvidenceCapability:
            {
                var payload = JsonSerializer.Deserialize<AiNativeSkillEvidencePayloadDto>(payloadJson, JsonOptions)!;
                var selectedContributors = payload.Contributors
                    .Where(item => item.Selected)
                    .Select(item => item.UserId)
                    .Distinct()
                    .ToArray();
                var evidence = await _skillEvidence.ReplaceTaskCompletionAttributionsAsync(
                    payload.TaskId,
                    new ReplaceTaskCompletionAttributionsDto(
                        payload.TaskRowVersion,
                        selectedContributors,
                        true,
                        "Confirmed from completed acceptance checklist through AI Native review."),
                    ct);
                if (!evidence.IsSuccess || evidence.Data == null)
                    return Result.Failure<IReadOnlyList<AiNativeActionReceiptItemDto>>(
                        evidence.Error ?? "Skill evidence confirmation failed.", evidence.StatusCode, evidence.ErrorCode);
                result.AddRange(evidence.Data.Attributions
                    .Where(item => string.Equals(item.Status, TaskCompletionAttribution.Confirmed, StringComparison.Ordinal))
                    .Select(item => new AiNativeActionReceiptItemDto(
                        "skill_evidence", item.Id, item.ContributorName,
                        $"/projects/{payload.ProjectId}/tasks/{payload.TaskId}")));
                break;
            }
        }
        return Result.Success<IReadOnlyList<AiNativeActionReceiptItemDto>>(result);
    }

    private async Task<Result<IReadOnlyList<AiNativeActionReceiptItemDto>>> VerifyCanonicalReadBackAsync(
        IReadOnlyList<AiNativeActionReceiptItemDto> items,
        CancellationToken ct)
    {
        foreach (var item in items)
        {
            var exists = item.EntityType switch
            {
                "acceptance_checklist_item" => await _db.TaskAcceptanceChecklistItems.AsNoTracking()
                    .AnyAsync(row => row.Id == item.EntityId, ct),
                "task" => await _db.TaskItems.AsNoTracking()
                    .AnyAsync(row => row.Id == item.EntityId, ct),
                "wiki_brief" => await _db.WikiPages.AsNoTracking()
                    .AnyAsync(row => row.Id == item.EntityId, ct),
                "group_poll" => await _db.GroupPolls.AsNoTracking()
                    .AnyAsync(row => row.Id == item.EntityId, ct),
                "project_digest_subscription" => await _db.ProjectDigestSubscriptions.AsNoTracking()
                    .AnyAsync(row => row.Id == item.EntityId, ct),
                "meeting_action_mapping" => await _db.MeetingActionItemMappings.AsNoTracking()
                    .AnyAsync(row => row.TaskId == item.EntityId, ct),
                "meeting_review" => await _db.MeetingImports.AsNoTracking()
                    .AnyAsync(row => row.Id == item.EntityId, ct),
                "sprint" => await _db.Set<Sprint>().AsNoTracking()
                    .AnyAsync(row => row.Id == item.EntityId, ct),
                "skill_evidence" => await _db.TaskCompletionAttributions.AsNoTracking()
                    .AnyAsync(row => row.Id == item.EntityId && row.Status == TaskCompletionAttribution.Confirmed, ct),
                _ => false
            };

            if (!exists)
            {
                return Result.Failure<IReadOnlyList<AiNativeActionReceiptItemDto>>(
                    $"Canonical read-back could not find {item.EntityType} {item.EntityId}.",
                    500,
                    "canonical_readback_failed");
            }
        }

        return Result.Success(items);
    }

    private async Task<SourceVersionResult> ResolveCurrentSourceVersionAsync(AiNativeActionDraft draft, Guid userId, CancellationToken ct)
    {
        if (draft.CapabilityId is AiNativeDomainActionContract.ChecklistCapability or AiNativeDomainActionContract.BreakdownCapability)
        {
            var task = await _db.TaskItems.AsNoTracking().Include(item => item.Project).ThenInclude(p => p.Members)
                .Include(item => item.Project).ThenInclude(p => p.Organization).ThenInclude(o => o!.Members)
                .Include(item => item.AcceptanceChecklist)
                .Include(item => item.Subtasks)
                .Include(item => item.SkillRequirements).ThenInclude(item => item.OrganizationSkill)
                .AsSplitQuery()
                .SingleOrDefaultAsync(item => item.Id == draft.TargetId, ct);
            if (task == null) return SourceVersionResult.Denied("Target Task no longer exists.");
            var permission = await ResolveProjectPermissionAsync(task.Project, userId, ct);
            return permission.CanManage
                ? SourceVersionResult.Allowed(TaskSourceVersion(task, draft.CapabilityId))
                : SourceVersionResult.Denied();
        }
        if (draft.CapabilityId == AiNativeDomainActionContract.WikiCapability)
        {
            var wiki = await _db.WikiPages.AsNoTracking().Include(item => item.Project).ThenInclude(p => p.Members)
                .Include(item => item.Project).ThenInclude(p => p.Organization).ThenInclude(o => o!.Members)
                .SingleOrDefaultAsync(item => item.Id == draft.TargetId, ct);
            if (wiki == null) return SourceVersionResult.Denied("Wiki page no longer exists.");
            var permission = await ResolveProjectPermissionAsync(wiki.Project, userId, ct);
            return permission.CanManage ? SourceVersionResult.Allowed(WikiSourceVersion(wiki)) : SourceVersionResult.Denied();
        }
        if (draft.CapabilityId == AiNativeDomainActionContract.GroupPollCapability)
        {
            var group = await _db.WorkGroups.AsNoTracking().Include(item => item.Members)
                .SingleOrDefaultAsync(item => item.Id == draft.TargetId, ct);
            if (group == null) return SourceVersionResult.Denied("Group no longer exists.");
            var role = group.OwnerId == userId ? GroupRoleRules.Owner : group.Members.FirstOrDefault(item => item.UserId == userId)?.Role;
            return GroupRoleRules.CanCreatePoll(role) ? SourceVersionResult.Allowed(GroupSourceVersion(group)) : SourceVersionResult.Denied();
        }
        if (draft.CapabilityId == AiNativeDomainActionContract.DigestCapability)
        {
            var project = await LoadProjectAsync(draft.TargetId, ct);
            if (project == null) return SourceVersionResult.Denied("Project no longer exists.");
            var permission = await ResolveProjectPermissionAsync(project, userId, ct);
            var subscription = await _db.ProjectDigestSubscriptions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ProjectId == project.Id && item.UserId == userId, ct);
            var organizationTimeZone = project.OrganizationId.HasValue
                ? await _db.OrganizationMemberCapacityProfiles.AsNoTracking()
                    .Where(item => item.OrganizationId == project.OrganizationId.Value && item.UserId == userId)
                    .Select(item => item.TimeZoneId)
                    .SingleOrDefaultAsync(ct)
                : null;
            return permission.CanManage
                ? SourceVersionResult.Allowed(DigestSourceVersion(project, subscription, organizationTimeZone))
                : SourceVersionResult.Denied();
        }
        if (draft.CapabilityId == AiNativeDomainActionContract.MeetingActionsCapability)
        {
            var meeting = await _db.MeetingImports.AsNoTracking()
                .Include(item => item.Project).ThenInclude(project => project.Members)
                .Include(item => item.Project).ThenInclude(project => project.Organization).ThenInclude(org => org!.Members)
                .Include(item => item.AiDraft)
                .Include(item => item.ActionItemMappings)
                .SingleOrDefaultAsync(item => item.Id == draft.TargetId, ct);
            if (meeting == null) return SourceVersionResult.Denied("Meeting import no longer exists.");
            var permission = await ResolveProjectPermissionAsync(meeting.Project, userId, ct);
            return permission.CanManage
                ? SourceVersionResult.Allowed(MeetingSourceVersion(meeting))
                : SourceVersionResult.Denied();
        }
        if (draft.CapabilityId == AiNativeDomainActionContract.RoadmapAdjustCapability)
        {
            var project = await LoadRoadmapProjectAsync(draft.TargetId, ct);
            if (project == null) return SourceVersionResult.Denied("Project no longer exists.");
            var permission = await ResolveProjectPermissionAsync(project, userId, ct);
            var projectMemberIds = project.Members.Select(member => member.UserId).ToArray();
            var capacity = project.OrganizationId.HasValue
                ? await _db.OrganizationMemberCapacityProfiles.AsNoTracking()
                    .Where(item => item.OrganizationId == project.OrganizationId.Value &&
                        projectMemberIds.Contains(item.UserId))
                    .SumAsync(item => (decimal?)item.WeeklyCapacityHours, ct) ?? 0m
                : 0m;
            return permission.CanManage
                ? SourceVersionResult.Allowed(RoadmapSourceVersion(project, capacity))
                : SourceVersionResult.Denied();
        }
        if (draft.CapabilityId == AiNativeDomainActionContract.SkillEvidenceCapability)
        {
            var task = await LoadSkillEvidenceTaskAsync(draft.TargetId, ct);
            if (task == null) return SourceVersionResult.Denied("Task no longer exists.");
            var permission = await ResolveProjectPermissionAsync(task.Project, userId, ct);
            return permission.CanManage
                ? SourceVersionResult.Allowed(SkillEvidenceSourceVersion(task))
                : SourceVersionResult.Denied();
        }
        return SourceVersionResult.Denied();
    }

    private async Task<TaskItem?> ResolveTaskAsync(AiAssistantClientContextDto? context, CancellationToken ct)
    {
        var id = string.Equals(context?.EntityType, "task", StringComparison.OrdinalIgnoreCase) ? context?.EntityId : null;
        return !id.HasValue ? null : await _db.TaskItems.AsNoTracking()
            .Include(item => item.Project).ThenInclude(p => p.Members)
            .Include(item => item.Project).ThenInclude(p => p.Organization).ThenInclude(o => o!.Members)
            .Include(item => item.AcceptanceChecklist)
            .Include(item => item.Subtasks)
            .Include(item => item.SkillRequirements).ThenInclude(item => item.OrganizationSkill)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == id.Value, ct);
    }

    private Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken ct)
        => _db.Projects.AsNoTracking().Include(item => item.Members)
            .Include(item => item.Organization).ThenInclude(o => o!.Members)
            .SingleOrDefaultAsync(item => item.Id == projectId, ct);

    private Task<Project?> LoadRoadmapProjectAsync(Guid projectId, CancellationToken ct)
        => _db.Projects.AsNoTracking()
            .Include(item => item.Members)
            .Include(item => item.Organization).ThenInclude(org => org!.Members)
            .Include(item => item.Sprints).ThenInclude(sprint => sprint.Tasks)
            .Include(item => item.Tasks).ThenInclude(task => task.PredecessorDependencies)
            .Include(item => item.Tasks).ThenInclude(task => task.SuccessorDependencies)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == projectId, ct);

    private Task<TaskItem?> LoadSkillEvidenceTaskAsync(Guid taskId, CancellationToken ct)
        => _db.TaskItems.AsNoTracking()
            .Include(item => item.Project).ThenInclude(project => project.Members)
            .Include(item => item.Project).ThenInclude(project => project.Organization).ThenInclude(org => org!.Members)
            .Include(item => item.Assignee)
            .Include(item => item.Assignees).ThenInclude(assignment => assignment.User)
            .Include(item => item.AcceptanceChecklist)
            .Include(item => item.SkillRequirements).ThenInclude(requirement => requirement.OrganizationSkill)
            .Include(item => item.CompletionAttributions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == taskId, ct);

    private Task<AiNativeProjectAuthorization> ResolveProjectPermissionAsync(Project project, Guid userId, CancellationToken ct)
        => _authorization.ResolveProjectAsync(project, userId, ProjectRoleRules.IsSystemAdmin(_currentUser.Role), ct);

    private static PreparedDraft Prepared(bool authorized, string? error, Guid? projectId, Guid? organizationId,
        string schemaId, string targetType, Guid targetId, string payloadJson, string sourceVersion)
        => new(authorized, error, projectId, organizationId, schemaId, targetType, targetId, payloadJson, sourceVersion);

    private static PayloadValidation ValidatePayload(string capabilityId, string json, Guid targetId)
    {
        try
        {
            return capabilityId switch
            {
                AiNativeDomainActionContract.ChecklistCapability => ValidateChecklist(JsonSerializer.Deserialize<AiNativeChecklistPayloadDto>(json, JsonOptions), targetId),
                AiNativeDomainActionContract.BreakdownCapability => ValidateBreakdown(JsonSerializer.Deserialize<AiNativeBreakdownPayloadDto>(json, JsonOptions), targetId),
                AiNativeDomainActionContract.WikiCapability => ValidateWiki(JsonSerializer.Deserialize<AiNativeWikiPayloadDto>(json, JsonOptions), targetId),
                AiNativeDomainActionContract.GroupPollCapability => ValidatePoll(JsonSerializer.Deserialize<AiNativeGroupPollPayloadDto>(json, JsonOptions), targetId),
                AiNativeDomainActionContract.DigestCapability => ValidateDigest(JsonSerializer.Deserialize<AiNativeDigestPayloadDto>(json, JsonOptions), targetId),
                AiNativeDomainActionContract.MeetingActionsCapability => ValidateMeetingActions(JsonSerializer.Deserialize<AiNativeMeetingActionsPayloadDto>(json, JsonOptions), targetId),
                AiNativeDomainActionContract.RoadmapAdjustCapability => ValidateRoadmapAdjustment(JsonSerializer.Deserialize<AiNativeRoadmapAdjustmentPayloadDto>(json, JsonOptions), targetId),
                AiNativeDomainActionContract.SkillEvidenceCapability => ValidateSkillEvidence(JsonSerializer.Deserialize<AiNativeSkillEvidencePayloadDto>(json, JsonOptions), targetId),
                _ => PayloadValidation.Invalid("Unsupported capability payload.")
            };
        }
        catch (JsonException)
        {
            return PayloadValidation.Invalid("Payload does not match the registered schema.");
        }
    }

    private static PayloadValidation ValidateChecklist(AiNativeChecklistPayloadDto? value, Guid targetId)
        => value == null || value.TaskId != targetId || value.Items.Count is < 1 or > 30 ||
           value.Items.Any(item => string.IsNullOrWhiteSpace(item) || item.Trim().Length > 1000)
            ? PayloadValidation.Invalid("Checklist must contain 1-30 valid items for the reviewed Task.") : PayloadValidation.Valid();

    private static PayloadValidation ValidateBreakdown(AiNativeBreakdownPayloadDto? value, Guid targetId)
        => value == null || value.ParentTaskId != targetId || value.Subtasks.Count is < 1 or > 20 ||
           value.SkillOptions is { Count: > 100 } ||
           value.SkillOptions?.Any(option => option.SkillId == Guid.Empty || string.IsNullOrWhiteSpace(option.Name) || option.Name.Length > 200) == true ||
           value.SkillOptions?.Select(option => option.SkillId).Distinct().Count() != value.SkillOptions?.Count ||
           value.Subtasks.Any(item => string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 300 ||
               item.Description?.Length > 8000 || item.EstimatedHours is < 0 or > 10000 ||
               item.RequiredSkillId.HasValue != !string.IsNullOrWhiteSpace(item.RequiredSkillName) ||
               item.RequiredSkillName?.Length > 200 ||
               item.RequiredSkillId.HasValue && value.SkillOptions is { Count: > 0 } &&
                   value.SkillOptions.All(option => option.SkillId != item.RequiredSkillId.Value) ||
               !ValidTaskPriorities.Contains(item.Priority)) ||
           value.Subtasks.Select(item => item.Title.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != value.Subtasks.Count
            ? PayloadValidation.Invalid("Breakdown must contain 1-20 valid subtasks for the reviewed Task.") : PayloadValidation.Valid();

    private static PayloadValidation ValidateWiki(AiNativeWikiPayloadDto? value, Guid targetId)
    {
        if (value == null || value.WikiPageId != targetId || string.IsNullOrWhiteSpace(value.Summary) || value.Summary.Length > 8000)
            return PayloadValidation.Invalid("Wiki brief is invalid.");
        if (value.SectionRefs is not { Count: > 0 } ||
            value.SectionRefs.Any(source => string.IsNullOrWhiteSpace(source) || source.Length > 1000))
            return PayloadValidation.Invalid("Wiki brief must retain at least one valid section source.");
        if (value.TaskCandidates != null)
        {
            if (value.TaskCandidates.Count is < 1 or > 3 ||
                value.TaskCandidates.Select(item => item.ClientId.Trim()).Distinct(StringComparer.Ordinal).Count() != value.TaskCandidates.Count ||
                value.TaskCandidates.Any(item =>
                    string.IsNullOrWhiteSpace(item.ClientId) || item.ClientId.Length > 80 ||
                    string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 300 ||
                    string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 16000 ||
                    string.IsNullOrWhiteSpace(item.SourceRef) || item.SourceRef.Length > 1000 ||
                    !value.SectionRefs.Contains(item.SourceRef, StringComparer.Ordinal)))
                return PayloadValidation.Invalid("Wiki Task candidates must contain 1-3 unique, sourced drafts.");
        }
        else if (value.CreateTask &&
                 (string.IsNullOrWhiteSpace(value.TaskTitle) || value.TaskTitle.Trim().Length > 300 ||
                  string.IsNullOrWhiteSpace(value.TaskDescription) || value.TaskDescription.Length > 16000))
        {
            return PayloadValidation.Invalid("The legacy Wiki Task draft is invalid.");
        }
        return PayloadValidation.Valid();
    }

    private static PayloadValidation ValidatePoll(AiNativeGroupPollPayloadDto? value, Guid targetId)
        => value == null || value.GroupId != targetId || string.IsNullOrWhiteSpace(value.Question) || value.Question.Trim().Length > 500 ||
           value.Options.Count is < 2 or > 10 || value.Options.Any(item => string.IsNullOrWhiteSpace(item) || item.Trim().Length > 300) ||
           value.Options.Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != value.Options.Count ||
           value.ExpiredAt.HasValue && value.ExpiredAt <= DateTimeOffset.UtcNow
            ? PayloadValidation.Invalid("Poll must contain one question and 2-10 unique options.") : PayloadValidation.Valid();

    private static PayloadValidation ValidateDigest(AiNativeDigestPayloadDto? value, Guid targetId)
        => value == null || value.ProjectId != targetId || value.DayOfWeek is < 0 or > 6 ||
           value.LocalTimeMinutes is < 0 or >= 1440 || string.IsNullOrWhiteSpace(value.TimeZoneId) || !IsKnownTimeZone(value.TimeZoneId) ||
           !string.Equals(value.DeliveryChannel, "email", StringComparison.OrdinalIgnoreCase)
            ? PayloadValidation.Invalid("Digest schedule is invalid.") : PayloadValidation.Valid();

    private static PayloadValidation ValidateMeetingActions(AiNativeMeetingActionsPayloadDto? value, Guid targetId)
        => value == null || value.MeetingImportId != targetId || string.IsNullOrWhiteSpace(value.Summary) ||
           value.Summary.Length > 16000 || value.ActionItems.Count > 100 ||
           value.ActionItems.Select(item => item.ItemIndex).Distinct().Count() != value.ActionItems.Count ||
           value.ActionItems.Any(item => item.ItemIndex < 0 || string.IsNullOrWhiteSpace(item.Title) ||
               item.Title.Length > 300 || item.Description?.Length > 16000 || item.SourceEvidence?.Length > 16000 ||
               !ValidTaskPriorities.Contains(item.Priority) ||
               item.MappingMode is not ("none" or "existing_task" or "new_task") ||
               (item.MappingMode == "existing_task") != item.ExistingTaskId.HasValue ||
               item.ExistingTaskId.HasValue && value.ExistingTaskOptions.All(option => option.TaskId != item.ExistingTaskId.Value))
            ? PayloadValidation.Invalid("Meeting review contains an invalid action-item mapping.")
            : PayloadValidation.Valid();

    private static PayloadValidation ValidateRoadmapAdjustment(AiNativeRoadmapAdjustmentPayloadDto? value, Guid targetId)
        => value == null || value.ProjectId != targetId || string.IsNullOrWhiteSpace(value.Summary) ||
           value.Adjustments.Count is < 1 or > 20 ||
           value.Adjustments.All(item => !item.Selected) ||
           value.Adjustments.Any(item => string.IsNullOrWhiteSpace(item.SprintName) || item.SprintName.Length > 200 ||
               item.AfterEnd <= item.AfterStart || item.BeforeEnd <= item.BeforeStart ||
               string.IsNullOrWhiteSpace(item.Reason) || item.Reason.Length > 2000) ||
           value.Adjustments.Where(item => item.SprintId.HasValue)
               .Select(item => item.SprintId!.Value).Distinct().Count() !=
           value.Adjustments.Count(item => item.SprintId.HasValue)
            ? PayloadValidation.Invalid("Roadmap adjustment must contain valid, unique before/after Sprint rows.")
            : PayloadValidation.Valid();

    private static PayloadValidation ValidateSkillEvidence(AiNativeSkillEvidencePayloadDto? value, Guid targetId)
        => value == null || value.TaskId != targetId || string.IsNullOrWhiteSpace(value.TaskRowVersion) ||
           value.Contributors.Count is < 1 or > 20 || value.Skills.Count is < 1 or > 30 ||
           value.AcceptanceEvidence.Count is < 1 or > 30 ||
           value.Contributors.Select(item => item.UserId).Distinct().Count() != value.Contributors.Count ||
           value.Skills.Select(item => item.SkillId).Distinct().Count() != value.Skills.Count ||
           value.Contributors.All(item => !item.Selected) ||
           value.Skills.Any(item => string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.RequiredLevel)) ||
           value.AcceptanceEvidence.Any(item => string.IsNullOrWhiteSpace(item))
            ? PayloadValidation.Invalid("Skill evidence must use selected assignees, confirmed skills and acceptance evidence.")
            : PayloadValidation.Valid();

    private static bool IsKnownTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    private static bool RequestedDigestEnabled(string message, bool current)
    {
        var normalized = NormalizeIntent(message);
        if (Regex.IsMatch(normalized, @"\b(tat|dung|ngung|huy|disable|disabled|off)\b", RegexOptions.IgnoreCase))
            return false;
        if (Regex.IsMatch(normalized, @"\b(bat|enable|enabled|on|dang ky)\b", RegexOptions.IgnoreCase))
            return true;
        return current;
    }

    private static string NormalizeIntent(string value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character == 'đ' ? 'd' : character == 'Đ' ? 'D' : character);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private static string[] BuildChecklist(TaskItem task, int count)
    {
        var specifics = ExtractSentences(task.Description).Take(3).ToList();
        var result = new List<string>
        {
            $"Kết quả '{task.Title}' đáp ứng phạm vi đã mô tả",
            "Luồng chính được kiểm thử end-to-end trên dữ liệu thật",
            "Không còn lỗi chặn và có bằng chứng/read-back để nghiệm thu"
        };
        result.AddRange(specifics.Select(item => $"Xác minh: {item}"));
        result.AddRange([
            "Quyền truy cập và dữ liệu nhạy cảm được kiểm tra theo đúng vai trò",
            "Trạng thái lỗi, retry và rollback được kiểm chứng",
            "Kết quả sau reload khớp dữ liệu canonical đã nghiệm thu",
            "Hiển thị và navigation hoạt động trên các kích thước hỗ trợ"
        ]);
        while (result.Distinct(StringComparer.OrdinalIgnoreCase).Count() < count)
            result.Add($"Tiêu chí nghiệm thu kiểm chứng được {result.Count + 1} cho '{task.Title}'");
        return result.Distinct(StringComparer.OrdinalIgnoreCase).Take(count).ToArray();
    }

    private static AiNativeBreakdownItemDto[] BuildSubtasks(
        TaskItem parent,
        string message,
        int count,
        IReadOnlyList<RequiredSkillOption> requiredSkills)
    {
        var named = ExtractCommaItems(message).Where(item => item.Length <= 160).Take(count).ToList();
        var phases = new[] { "Khảo sát và xác nhận phạm vi", "Thiết kế giải pháp", "Triển khai", "Kiểm thử tích hợp", "Nghiệm thu và bàn giao", "Theo dõi sau phát hành" };
        // Requested counts can exceed the phase template count. Reusing the raw
        // phase label produced duplicate titles, so the draft passed preparation
        // but was rejected by the confirm-time uniqueness validator (for example,
        // an exact request for 10 subtasks). Give every generated phase a stable
        // ordinal; user-provided distinct names remain untouched.
        while (named.Count < count)
        {
            var position = named.Count;
            named.Add($"Bước {position + 1}: {phases[position % phases.Length]}");
        }
        return named.Take(count).Select((name, index) =>
        {
            var skill = requiredSkills.Count == 0 ? null : requiredSkills[index % requiredSkills.Count];
            return new AiNativeBreakdownItemDto(
                $"{name}: {parent.Title}", $"Giai đoạn {index + 1} của task cha '{parent.Title}'.", parent.Priority,
                AllocateEstimate(parent.EstimatedHours, count, index), index > 0,
                skill?.SkillId, skill?.Name);
        }).ToArray();
    }

    private static int? AllocateEstimate(int? totalHours, int count, int index)
    {
        if (!totalHours.HasValue || totalHours.Value <= 0 || count <= 0) return null;
        var hours = totalHours.Value / count + (index < totalHours.Value % count ? 1 : 0);
        return hours > 0 ? hours : null;
    }

    private static int RequestedCount(string message, int fallback)
    {
        var match = CountRegex().Match(message);
        return match.Success && int.TryParse(match.Groups[1].Value, out var count) ? Math.Clamp(count, 1, 20) : fallback;
    }

    private static int RequestedOptionCount(string message, int fallback)
    {
        var match = OptionCountRegex().Match(NormalizeIntent(message));
        return match.Success && int.TryParse(match.Groups[1].Value, out var count)
            ? Math.Clamp(count, 2, 10)
            : fallback;
    }

    private static DateTimeOffset? RequestedExpiry(string message)
    {
        var match = DeadlineDaysRegex().Match(NormalizeIntent(message));
        return match.Success && int.TryParse(match.Groups[1].Value, out var days)
            ? DateTimeOffset.UtcNow.AddDays(Math.Clamp(days, 1, 365))
            : null;
    }

    private static string[] ExtractCommaItems(string text)
    {
        var marker = text.IndexOf(':');
        var source = marker >= 0 ? text[(marker + 1)..] : text;
        return source.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> ExtractSentences(string? value)
        => string.IsNullOrWhiteSpace(value) ? [] : value.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length is >= 8 and <= 300);

    private static (string Question, IReadOnlyList<string> Options) ParseGroupPoll(
        string message,
        string fallbackQuestion,
        int requestedCount)
    {
        var text = message.Trim();
        var colon = text.IndexOf(':');
        var content = colon >= 0 ? text[(colon + 1)..].Trim() : text;
        var question = fallbackQuestion;
        var optionSource = content;
        var questionMark = content.IndexOf('?');
        if (questionMark >= 4)
        {
            var candidate = content[..(questionMark + 1)].Trim();
            if (candidate.Length <= 500) question = candidate;
            optionSource = content[(questionMark + 1)..].TrimStart(' ', ':', '-', '–', '—');
        }

        // With the common shorthand "Tạo poll: A, B, C" the text after the
        // colon is the option list, not a question. When no explicit question
        // mark exists, keep the meaningful server fallback instead of turning
        // the command phrase "Tạo poll" into a fake question.
        var options = optionSource
            .Split([',', ';', '\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim(' ', '-', '–', '—', '.', '?'))
            .Where(item => item.Length is >= 1 and <= 300)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        if (options.Count < 2)
        {
            var commonDeploymentOptions = new[]
            {
                "Thử nghiệm giới hạn rồi mở rộng",
                "Triển khai theo từng giai đoạn",
                "Triển khai toàn bộ trong một đợt",
                "Tạm hoãn để xử lý rủi ro"
            };
            options = commonDeploymentOptions.Take(requestedCount).ToList();
        }
        while (options.Count < requestedCount)
            options.Add($"Phương án tùy chỉnh {options.Count + 1}");
        if (options.Count > requestedCount) options = options.Take(requestedCount).ToList();
        return (question, options);
    }

    private static AiNativeWikiTaskCandidateDto[] BuildWikiTaskCandidates(
        string wikiTitle,
        string summary,
        IReadOnlyList<string> sectionRefs)
    {
        return sectionRefs.Take(3).Select((sourceRef, index) =>
        {
            var separator = sourceRef.IndexOf(':');
            var section = separator >= 0 && separator + 1 < sourceRef.Length
                ? sourceRef[(separator + 1)..].Trim()
                : $"phần {index + 1}";
            return new AiNativeWikiTaskCandidateDto(
                $"wiki-task-{index + 1}",
                $"Theo dõi {section}",
                $"Xác minh và hoàn tất nội dung liên quan đến '{section}' từ Wiki '{wikiTitle}'.\n\n{summary}",
                false,
                sourceRef);
        }).ToArray();
    }

    private static AiNativeRoadmapAdjustmentItemDto[] BuildRoadmapAdjustments(Project project, decimal weeklyCapacity)
    {
        if (project.Sprints.Count == 0)
        {
            var start = project.StartDate ?? DateTimeOffset.UtcNow.Date;
            var estimatedHours = project.Tasks.Where(task => !task.IsDeleted && task.Status != "Done")
                .Sum(task => task.EstimatedHours ?? 0);
            var weeks = weeklyCapacity > 0
                ? Math.Max(1, (int)Math.Ceiling(estimatedHours / (double)weeklyCapacity))
                : 2;
            var end = project.EndDate ?? start.AddDays(weeks * 7);
            if (end <= start) end = start.AddDays(7);
            return
            [
                new AiNativeRoadmapAdjustmentItemDto(
                    null,
                    "Sprint đề xuất 1",
                    start,
                    end,
                    start,
                    end,
                    false,
                    weeklyCapacity > 0
                        ? $"Chưa có Sprint; {estimatedHours} giờ việc mở được đối chiếu với {weeklyCapacity:0.#} giờ capacity/tuần."
                        : "Chưa có Sprint và chưa đủ capacity đã khai báo; cần review thủ công trước khi chọn.")
            ];
        }

        return project.Sprints.OrderBy(sprint => sprint.StartDate).Take(20).Select(sprint =>
        {
            var openTasks = sprint.Tasks.Where(task => !task.IsDeleted && task.Status != "Done").ToArray();
            var estimatedHours = openTasks.Sum(task => task.EstimatedHours ?? 0);
            var dependencyCount = openTasks.Sum(task => task.PredecessorDependencies.Count);
            var latestTaskDeadline = openTasks.Where(task => task.DueDate.HasValue)
                .Select(task => task.DueDate!.Value)
                .DefaultIfEmpty(sprint.EndDate)
                .Max();
            var capacityEnd = weeklyCapacity > 0
                ? sprint.StartDate.AddDays(Math.Max(7, Math.Ceiling(estimatedHours / (double)weeklyCapacity) * 7))
                : sprint.EndDate;
            var proposedEnd = new[] { sprint.EndDate, latestTaskDeadline, capacityEnd }.Max();
            if (proposedEnd <= sprint.StartDate) proposedEnd = sprint.StartDate.AddDays(7);
            var reasons = new List<string>
            {
                $"{openTasks.Length} Task mở / {estimatedHours} giờ",
                $"{dependencyCount} dependency",
                weeklyCapacity > 0 ? $"{weeklyCapacity:0.#} giờ capacity/tuần đã khai báo" : "capacity chưa được khai báo đầy đủ",
                $"deadline muộn nhất {latestTaskDeadline:dd/MM/yyyy}"
            };
            return new AiNativeRoadmapAdjustmentItemDto(
                sprint.Id,
                sprint.Name,
                sprint.StartDate,
                sprint.EndDate,
                sprint.StartDate,
                proposedEnd,
                false,
                string.Join("; ", reasons));
        }).ToArray();
    }

    private static DigestSchedule ParseDigestSchedule(
        string message,
        int fallbackDayOfWeek,
        int fallbackLocalTimeMinutes,
        string fallbackTimeZoneId)
    {
        var normalized = NormalizeIntent(message);
        var day = fallbackDayOfWeek;
        if (ContainsAny(normalized, "thu hai", "monday")) day = 1;
        else if (ContainsAny(normalized, "thu ba", "tuesday")) day = 2;
        else if (ContainsAny(normalized, "thu tu", "wednesday")) day = 3;
        else if (ContainsAny(normalized, "thu nam", "thursday")) day = 4;
        else if (ContainsAny(normalized, "thu sau", "friday")) day = 5;
        else if (ContainsAny(normalized, "thu bay", "saturday")) day = 6;
        else if (ContainsAny(normalized, "chu nhat", "sunday")) day = 0;

        var minutes = fallbackLocalTimeMinutes;
        var timeMatch = ClockTimeRegex().Match(normalized);
        if (timeMatch.Success &&
            int.TryParse(timeMatch.Groups[1].Value, out var hour) &&
            int.TryParse(timeMatch.Groups[2].Value, out var minute) &&
            hour is >= 0 and <= 23 && minute is >= 0 and <= 59)
        {
            minutes = hour * 60 + minute;
        }
        return new DigestSchedule(day, minutes, fallbackTimeZoneId);
    }

    private static bool ContainsAny(string value, params string[] terms)
        => terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string Summarize(string content)
    {
        var plain = MarkdownRegex().Replace(content, " ");
        plain = WhitespaceRegex().Replace(plain, " ").Trim();
        return plain.Length <= 1200 ? plain : plain[..1200] + "…";
    }

    private static string NormalizePriority(string value)
        => value.Trim().ToLowerInvariant() switch { "low" => "Low", "high" => "High", "critical" => "Critical", _ => "Medium" };

    private static string[] ExtractWikiSections(string content, string sourceRef)
    {
        var headings = content.Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith('#'))
            .Select(line => line.TrimStart('#', ' '))
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
        if (headings.Length == 0) return [$"{sourceRef}#section-1"];
        return headings.Select((heading, index) => $"{sourceRef}#section-{index + 1}:{heading}").ToArray();
    }

    private static DateTimeOffset NextDelivery(AiNativeDigestPayloadDto payload)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(payload.TimeZoneId); }
        catch { zone = TimeZoneInfo.CreateCustomTimeZone("Qaly-SE-Asia", TimeSpan.FromHours(7), "Qaly SE Asia", "Qaly SE Asia"); }
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var days = (payload.DayOfWeek - (int)nowLocal.DayOfWeek + 7) % 7;
        var candidate = nowLocal.Date.AddDays(days).AddMinutes(payload.LocalTimeMinutes);
        if (candidate <= nowLocal.DateTime) candidate = candidate.AddDays(7);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified), zone);
    }

    private static string TaskSourceVersion(TaskItem task, string capabilityId)
    {
        var related = capabilityId == AiNativeDomainActionContract.ChecklistCapability
            ? string.Join('|', task.AcceptanceChecklist.OrderBy(item => item.SortOrder).ThenBy(item => item.Id)
                .Select(item => $"{item.Id}:{item.SortOrder}:{item.IsCompleted}:{item.Text}:{Convert.ToBase64String(item.RowVersion)}"))
            : string.Join('|', task.Subtasks.OrderBy(item => item.SortOrder).ThenBy(item => item.Id)
                .Select(item => $"{item.Id}:{item.SortOrder}:{item.Status}:{item.Title}:{item.ContributesToProgress}:{Convert.ToBase64String(item.RowVersion)}"));
        var requiredSkills = string.Join('|', task.SkillRequirements
            .OrderBy(item => item.OrganizationSkillId)
            .Select(item => $"{item.OrganizationSkillId}:{item.RequiredLevel}:{item.OrganizationSkill.IsActive}:{item.OrganizationSkill.Name}:{Convert.ToBase64String(item.RowVersion)}"));
        return Hash($"task:{task.Id}:{task.UpdatedAt ?? task.CreatedAt}:{task.ContributesToProgress}:{Convert.ToBase64String(task.RowVersion)}:{related}:{requiredSkills}");
    }
    private static string WikiSourceVersion(WikiPage page) => Hash($"wiki:{page.Id}:{page.UpdatedAt}:{page.Visibility}:{page.Content}");
    private static string GroupSourceVersion(WorkGroup group) => Hash($"group:{group.Id}:{group.UpdatedAt ?? group.CreatedAt}:{group.Status}:{group.Members.Count}");
    private static string ProjectSourceVersion(Project project) => Hash($"project:{project.Id}:{project.UpdatedAt ?? project.CreatedAt}:{project.Status}:{project.Members.Count}");
    private static string MeetingSourceVersion(MeetingImport meeting)
    {
        var mappings = string.Join('|', meeting.ActionItemMappings.OrderBy(item => item.ActionItemIndex)
            .Select(item => $"{item.ActionItemIndex}:{item.TaskId}:{item.Status}:{item.UpdatedAt ?? item.CreatedAt}"));
        var draft = meeting.AiDraft == null
            ? string.Empty
            : $"{meeting.AiDraft.Id}:{meeting.AiDraft.UpdatedAt ?? meeting.AiDraft.CreatedAt}:{meeting.AiDraft.WorkingPayloadJson}:{meeting.AiDraft.PayloadJson}";
        return Hash($"meeting:{meeting.Id}:{meeting.PrivacyState}:{meeting.ContentRedactedAt}:{meeting.ContentDeletedAt}:{meeting.TranscriptText}:{draft}:{mappings}");
    }
    private static string RoadmapSourceVersion(Project project, decimal weeklyCapacity)
    {
        var sprints = string.Join('|', project.Sprints.OrderBy(item => item.Id)
            .Select(item => $"{item.Id}:{item.Name}:{item.StartDate}:{item.EndDate}:{item.Status}:{item.UpdatedAt ?? item.CreatedAt}"));
        var tasks = string.Join('|', project.Tasks.OrderBy(item => item.Id).Select(item =>
            $"{item.Id}:{item.SprintId}:{item.Status}:{item.EstimatedHours}:{item.StartDate}:{item.DueDate}:{item.AssigneeId}:{Convert.ToBase64String(item.RowVersion)}"));
        var dependencies = string.Join('|', project.Tasks.SelectMany(item => item.PredecessorDependencies)
            .OrderBy(item => item.PredecessorId).ThenBy(item => item.SuccessorId)
            .Select(item => $"{item.PredecessorId}:{item.SuccessorId}:{item.DependencyType}"));
        return Hash($"{ProjectSourceVersion(project)}:roadmap:{weeklyCapacity}:{sprints}:{tasks}:{dependencies}");
    }
    private static string SkillEvidenceSourceVersion(TaskItem task)
    {
        var checklist = string.Join('|', task.AcceptanceChecklist.OrderBy(item => item.SortOrder)
            .Select(item => $"{item.Id}:{item.IsCompleted}:{item.Text}:{Convert.ToBase64String(item.RowVersion)}"));
        var skills = string.Join('|', task.SkillRequirements.OrderBy(item => item.OrganizationSkillId)
            .Select(item => $"{item.OrganizationSkillId}:{item.RequiredLevel}:{item.OrganizationSkill.IsActive}:{Convert.ToBase64String(item.RowVersion)}"));
        var assignees = string.Join('|', task.Assignees.OrderBy(item => item.UserId)
            .Select(item => $"{item.UserId}:{item.AssignedAt}:{item.UpdatedAt ?? item.CreatedAt}"));
        var attributions = string.Join('|', task.CompletionAttributions.OrderBy(item => item.Id)
            .Select(item => $"{item.Id}:{item.ContributorUserId}:{item.Status}:{item.ConfirmedAt}:{Convert.ToBase64String(item.RowVersion)}"));
        return Hash($"skill-evidence:{task.Id}:{task.Status}:{task.AssigneeId}:{Convert.ToBase64String(task.RowVersion)}:{checklist}:{skills}:{assignees}:{attributions}");
    }
    private static string DigestSourceVersion(
        Project project,
        ProjectDigestSubscription? subscription,
        string? organizationTimeZone)
        => Hash($"{ProjectSourceVersion(project)}:digest:{subscription?.Id}:{subscription?.Revision}:{subscription?.IsEnabled}:{subscription?.DayOfWeek}:{subscription?.LocalTimeMinutes}:{subscription?.TimeZoneId}:{organizationTimeZone}:{Convert.ToBase64String(subscription?.RowVersion ?? Array.Empty<byte>())}");
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string RevisionToken(AiNativeActionDraft draft) => Convert.ToBase64String(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{draft.Id}:{draft.Revision}:{draft.SourceVersion}"))[..16]);

    private static AiNativeActionDraftDto ToDto(AiNativeActionDraft draft)
    {
        using var document = JsonDocument.Parse(draft.PayloadJson);
        var receipt = string.IsNullOrWhiteSpace(draft.ReceiptJson) ? null : JsonSerializer.Deserialize<AiNativeActionReceiptDto>(draft.ReceiptJson, JsonOptions);
        return new AiNativeActionDraftDto(draft.Id, draft.CapabilityId, draft.SchemaId, draft.RendererId,
            draft.TargetType, draft.TargetId, draft.ProjectId, draft.Status, draft.Revision, RevisionToken(draft),
            document.RootElement.Clone(), draft.SourceVersion, draft.ExpiresAt, draft.CreatedAt, receipt);
    }

    private async Task PersistDraftInAssistantTurnAsync(AiNativeActionDraft draft, CancellationToken ct)
    {
        var turnIds = await _db.AssistantArtifactRefs.AsNoTracking()
            .Where(item => item.DraftId == draft.Id)
            .Select(item => item.TurnId)
            .Distinct()
            .ToListAsync(ct);
        if (turnIds.Count == 0) return;

        var turns = await _db.AssistantTurns
            .Where(item => turnIds.Contains(item.Id))
            .ToListAsync(ct);
        var sessionIds = turns.Select(item => item.SessionId).Distinct().ToArray();
        var sessions = await _db.AssistantSessions
            .Where(item => sessionIds.Contains(item.Id))
            .ToListAsync(ct);
        var draftDto = ToDto(draft);
        foreach (var turn in turns)
        {
            if (string.IsNullOrWhiteSpace(turn.ResponseJson)) continue;
            try
            {
                var response = JsonSerializer.Deserialize<AiAssistantTurnResponseDto>(turn.ResponseJson, JsonOptions);
                if (response?.NativeActionDraft?.DraftId != draft.Id) continue;
                turn.ResponseJson = JsonSerializer.Serialize(response with { NativeActionDraft = draftDto }, JsonOptions);
            }
            catch (JsonException)
            {
                // A corrupt historical response must not block the canonical action.
                // The draft remains directly readable and the corrupt turn can be
                // diagnosed independently through its persisted audit trail.
            }
        }
        foreach (var session in sessions) session.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static ProjectDigestSubscriptionDto ToDto(ProjectDigestSubscription value)
        => new(value.Id, value.ProjectId, value.UserId, value.IsEnabled, value.Cadence, value.DayOfWeek,
            value.LocalTimeMinutes, value.TimeZoneId, value.NextDeliveryAt, value.LastDeliveryAt,
            value.LastDeliveryStatus, value.LastError, value.Revision, Convert.ToBase64String(value.RowVersion),
            value.LastDeliveryKey, value.LastAttemptAt, value.ConsecutiveFailureCount);

    private static TaskAcceptanceChecklistItemDto ToChecklistDto(TaskAcceptanceChecklistItem item)
        => new(item.Id, item.TaskId, item.Text, item.SortOrder, item.IsCompleted, ChecklistRevisionToken(item), item.Kind);

    private static string ChecklistRevisionToken(TaskAcceptanceChecklistItem item)
    {
        if (item.RowVersion.Length > 0) return Convert.ToBase64String(item.RowVersion);
        return Hash($"checklist:{item.Id:N}:{item.UpdatedAt?.UtcTicks ?? item.CreatedAt.UtcTicks}:{item.IsCompleted}:{item.Text}");
    }

    private static AiAuditEvent Audit(Guid userId, Guid? projectId, string eventType, AiNativeActionDraft draft, string outcome, string? after = null)
        => new()
        {
            ActorUserId = userId, ProjectId = projectId, EventType = eventType, EntityType = nameof(AiNativeActionDraft),
            EntityGuid = draft.Id, EntityKey = draft.CapabilityId, Purpose = "ai_native_confirmed_action",
            PolicyVersion = "ai-native-action.v1", DataClassification = "internal", ProviderClass = "deterministic",
            Outcome = outcome, AfterJson = after
        };

    [GeneratedRegex(@"(?<!\d)(\d{1,2})\s*(?:task|subtask|công việc|nhiệm vụ|mục|item|checklist)", RegexOptions.IgnoreCase)]
    private static partial Regex CountRegex();
    [GeneratedRegex(@"(?<!\d)(\d{1,2})\s*(?:option|options|lua chon|phuong an)", RegexOptions.IgnoreCase)]
    private static partial Regex OptionCountRegex();
    [GeneratedRegex(@"(?<!\d)(\d{1,3})\s*(?:ngay|day|days)", RegexOptions.IgnoreCase)]
    private static partial Regex DeadlineDaysRegex();
    [GeneratedRegex(@"(?<!\d)([01]?\d|2[0-3])[:h]([0-5]\d)(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex ClockTimeRegex();
    [GeneratedRegex(@"[#*_>`~\[\](){}|=-]+")]
    private static partial Regex MarkdownRegex();
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record PreparedDraft(bool Authorized, string? Error, Guid? ProjectId, Guid? OrganizationId,
        string SchemaId, string TargetType, Guid TargetId, string PayloadJson, string SourceVersion);
    private sealed record DigestSchedule(int DayOfWeek, int LocalTimeMinutes, string TimeZoneId);
    private sealed record RequiredSkillOption(Guid SkillId, string Name);
    private sealed record SourceVersionResult(bool Authorized, string Version, string? Error)
    {
        public static SourceVersionResult Allowed(string version) => new(true, version, null);
        public static SourceVersionResult Denied(string? error = null) => new(false, string.Empty, error);
    }
    private sealed record PayloadValidation(bool IsValid, string? Error)
    {
        public static PayloadValidation Valid() => new(true, null);
        public static PayloadValidation Invalid(string error) => new(false, error);
    }
}
