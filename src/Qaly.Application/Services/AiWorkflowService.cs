using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qaly.Application.Services;

public class AiWorkflowService : IAiWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _projectMemberRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<AiJob> _aiJobRepo;
    private readonly IRepository<AiGeneratedDraft> _aiDraftRepo;
    private readonly IRepository<MeetingImport> _meetingImportRepo;
    private readonly IRepository<MeetingActionItemMapping> _meetingActionItemMappingRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<TaskAssignment> _taskAssignmentRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly ITaskService? _taskService;
    private readonly ICommentService? _commentService;
    private readonly ITimeTrackingService? _timeTrackingService;
    private readonly IAiComplianceService? _complianceService;
    private readonly IAiCostService? _costService;
    private readonly IRepository<AiJobDispatch>? _aiDispatchRepo;
    private readonly IRepository<AiJobSource>? _aiSourceRepo;
    private readonly IAiSourceGuard? _sourceGuard;
    private readonly IOptionsMonitor<AiJobPlatformOptions>? _platformOptions;

    public AiWorkflowService(
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> projectMemberRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<AiJob> aiJobRepo,
        IRepository<AiGeneratedDraft> aiDraftRepo,
        IRepository<MeetingImport> meetingImportRepo,
        IRepository<MeetingActionItemMapping> meetingActionItemMappingRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<TaskAssignment> taskAssignmentRepo,
        IRepository<User> userRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        ITaskService? taskService = null,
        ICommentService? commentService = null,
        ITimeTrackingService? timeTrackingService = null,
        IAiComplianceService? complianceService = null,
        IAiCostService? costService = null,
        IRepository<AiJobDispatch>? aiDispatchRepo = null,
        IRepository<AiJobSource>? aiSourceRepo = null,
        IAiSourceGuard? sourceGuard = null,
        IOptionsMonitor<AiJobPlatformOptions>? platformOptions = null)
    {
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _aiJobRepo = aiJobRepo;
        _aiDraftRepo = aiDraftRepo;
        _meetingImportRepo = meetingImportRepo;
        _meetingActionItemMappingRepo = meetingActionItemMappingRepo;
        _taskRepo = taskRepo;
        _taskAssignmentRepo = taskAssignmentRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _taskService = taskService;
        _commentService = commentService;
        _timeTrackingService = timeTrackingService;
        _complianceService = complianceService;
        _costService = costService;
        _aiDispatchRepo = aiDispatchRepo;
        _aiSourceRepo = aiSourceRepo;
        _sourceGuard = sourceGuard;
        _platformOptions = platformOptions;
    }

    public async Task<Result<AiJobCreatedDto>> CreateJobAsync(
        CreateAiJobDto dto,
        string idempotencyKey,
        string? requestId = null,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<AiJobCreatedDto>();
        }

        if (_platformOptions != null && !_platformOptions.CurrentValue.Enabled)
        {
            return Result.Failure<AiJobCreatedDto>(
                "The canonical AI job platform is disabled.",
                503,
                AiErrorCodes.PlatformDisabled);
        }

        if (string.IsNullOrWhiteSpace(dto.JobType))
        {
            return Result.Failure<AiJobCreatedDto>("job_type is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result.Failure<AiJobCreatedDto>(
                "Idempotency-Key is required.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        if (dto.ProjectId == null)
        {
            return Result.Failure<AiJobCreatedDto>(
                "project_id is required for the current P0 AI functions.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        var sourceInputs = NormalizeSources(dto);
        if (sourceInputs.Count == 0 || sourceInputs.Any(source =>
                string.IsNullOrWhiteSpace(source.SourceType) ||
                (string.IsNullOrWhiteSpace(source.SourceVersion) && string.IsNullOrWhiteSpace(source.SourceHash))))
        {
            return Result.Failure<AiJobCreatedDto>(
                "Every source requires a type and source_version or source_hash.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        var schemaId = ResolveSchemaId(dto.JobType, dto.SchemaId);
        if (string.IsNullOrWhiteSpace(schemaId) || string.IsNullOrWhiteSpace(dto.SchemaVersion))
        {
            return Result.Failure<AiJobCreatedDto>(
                "schema_id and schema_version are required.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        var project = await _projectRepo.GetQueryable()
            .Include(item => item.Organization)
            .FirstOrDefaultAsync(item => item.Id == dto.ProjectId.Value, ct);
        if (project == null)
        {
            return Result.Failure<AiJobCreatedDto>("Project was not found.", 404);
        }

        if (!await CanAccessProjectAsync(project, currentUserId.Value, ct))
        {
            return Result.Forbidden<AiJobCreatedDto>();
        }

        if (_sourceGuard != null)
        {
            var sourceValidation = await _sourceGuard.ValidateAsync(
                project.Id,
                currentUserId.Value,
                sourceInputs,
                enforceFreshness: true,
                ct);
            if (!sourceValidation.IsAllowed)
            {
                return Result.Failure<AiJobCreatedDto>(
                    sourceValidation.ErrorMessage ?? "The AI source is not available.",
                    sourceValidation.ErrorCode == AiErrorCodes.SourceStale ? 409 : 403,
                    sourceValidation.ErrorCode);
            }
        }

        var tenantId = project.OrganizationId ?? project.Id;
        PrivacyProcessingDecision? privacyDecision = null;
        if (_complianceService != null && dto.Sensitive)
        {
            privacyDecision = await _complianceService.EvaluateProcessingAsync(new PrivacyProcessingRequest
            {
                TenantId = tenantId,
                ProjectId = project.Id,
                UserId = currentUserId.Value,
                Purpose = PrivacyPurposeForJob(dto.JobType),
                DataClassification = PrivacyDataClasses.SensitiveCollaboration,
                ProviderClass = ProviderClassForHint(dto.ProviderHint),
                ConsentId = dto.ConsentId,
                RetentionPolicyId = dto.RetentionPolicyId,
                SourceType = sourceInputs[0].SourceType.Trim(),
                SourceEntityId = sourceInputs[0].SourceEntityId
            }, ct);
            if (!privacyDecision.Allowed)
            {
                return Result.Failure<AiJobCreatedDto>(
                    privacyDecision.Reason,
                    403,
                    IsConsentError(privacyDecision.ErrorCode)
                        ? AiErrorCodes.ConsentRequired
                        : AiErrorCodes.SensitiveBlocked);
            }
        }

        if (_costService != null && !await _costService.EnsureBudgetAvailableAsync(project.OrganizationId, project.Id, ct))
        {
            return Result.Failure<AiJobCreatedDto>(
                "The effective AI budget has been exceeded.",
                429,
                AiErrorCodes.BudgetExceeded);
        }

        var estimatedCost = EstimateCost(dto.SourceText);
        if (dto.MaximumEstimatedCostUsd.HasValue && estimatedCost > dto.MaximumEstimatedCostUsd.Value)
        {
            return Result.Failure<AiJobCreatedDto>(
                "The request exceeds maximum_estimated_cost_usd.",
                402,
                AiErrorCodes.BudgetExceeded);
        }

        var requestJson = JsonSerializer.Serialize(new
        {
            jobType = dto.JobType.Trim(),
            projectId = project.Id,
            sources = sourceInputs,
            sourceText = dto.SourceText,
            providerHint = NormalizeProviderHint(dto.ProviderHint),
            schemaId,
            schemaVersion = dto.SchemaVersion.Trim(),
            cacheMode = dto.CacheMode,
            language = dto.Language,
            consentId = dto.ConsentId,
            retentionPolicyId = dto.RetentionPolicyId,
            privacyPurpose = dto.Sensitive ? PrivacyPurposeForJob(dto.JobType) : null,
            providerClass = dto.Sensitive ? ProviderClassForHint(dto.ProviderHint) : null,
            options = dto.Options
        }, JsonOptions);
        var requestHash = ComputeHash(requestJson);
        var normalizedIdempotencyKey = idempotencyKey.Trim();

        var existingJob = await _aiJobRepo.GetQueryable()
            .FirstOrDefaultAsync(job =>
                job.RequestedById == currentUserId.Value &&
                job.IdempotencyKey == normalizedIdempotencyKey,
                ct);
        if (existingJob != null)
        {
            if (!string.Equals(existingJob.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return Result.Failure<AiJobCreatedDto>(
                    "The idempotency key was already used for a different request.",
                    409,
                    AiErrorCodes.IdempotencyConflict);
            }

            return Result.Accepted(ToCreatedDto(existingJob, requestId));
        }

        var cacheKey = GenerateCacheKey(dto.JobType, project.Id, dto.SourceType, dto.SourceId, dto.SourceText);
        var now = DateTimeOffset.UtcNow;

        var job = new AiJob
        {
            TenantId = tenantId,
            JobType = dto.JobType.Trim(),
            ProjectId = project.Id,
            SourceType = sourceInputs[0].SourceType.Trim(),
            SourceId = string.IsNullOrWhiteSpace(dto.SourceId) ? null : dto.SourceId.Trim(),
            SchemaId = schemaId,
            SchemaVersion = dto.SchemaVersion.Trim(),
            RequestJson = requestJson,
            RequestHash = requestHash,
            IdempotencyKey = normalizedIdempotencyKey,
            ProviderHint = NormalizeProviderHint(dto.ProviderHint),
            Sensitive = dto.Sensitive,
            ConsentId = dto.ConsentId,
            RetentionPolicyId = dto.RetentionPolicyId,
            CloudEligible = !dto.Sensitive || privacyDecision?.CloudEligible == true,
            PolicyCheckedAt = now,
            PolicyDecisionJson = JsonSerializer.Serialize(new
            {
                checkedAt = now,
                sensitive = dto.Sensitive,
                consentId = dto.ConsentId,
                retentionPolicyId = dto.RetentionPolicyId,
                policyVersion = privacyDecision?.PolicyVersion,
                purpose = dto.Sensitive ? PrivacyPurposeForJob(dto.JobType) : null,
                providerClass = dto.Sensitive ? ProviderClassForHint(dto.ProviderHint) : null,
                cloudEligible = !dto.Sensitive || privacyDecision?.CloudEligible == true,
                localEligible = !dto.Sensitive || privacyDecision?.LocalEligible == true
            }),
            Status = AiJobStatuses.Queued,
            AvailableAt = now,
            MaxAttempts = Math.Max(1, _platformOptions?.CurrentValue.MaxAttempts ?? 3),
            EstimatedCostUsd = estimatedCost,
            MaximumCostUsd = dto.MaximumEstimatedCostUsd,
            CacheKey = cacheKey,
            RequestedById = currentUserId.Value
        };

        await _aiJobRepo.AddAsync(job, ct);
        foreach (var (source, index) in sourceInputs.Select((source, index) => (source, index)))
        {
            await RequireSourceRepository().AddAsync(new AiJobSource
            {
                AiJobId = job.Id,
                SourceType = source.SourceType.Trim(),
                SourceEntityId = source.SourceEntityId,
                LegacySourceKey = NormalizeOptional(source.LegacySourceKey),
                SourceVersion = NormalizeOptional(source.SourceVersion),
                SourceHash = NormalizeOptional(source.SourceHash),
                SourceTimestamp = source.SourceTimestamp,
                SortOrder = index
            }, ct);
        }

        await RequireDispatchRepository().AddAsync(new AiJobDispatch
        {
            AiJobId = job.Id,
            AvailableAt = now,
            Priority = 100
        }, ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var concurrentJob = await _aiJobRepo.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate =>
                    candidate.RequestedById == currentUserId.Value &&
                    candidate.IdempotencyKey == normalizedIdempotencyKey,
                    ct);
            if (concurrentJob == null) throw;
            if (!string.Equals(concurrentJob.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return Result.Failure<AiJobCreatedDto>(
                    "The idempotency key was concurrently used for a different request.",
                    409,
                    AiErrorCodes.IdempotencyConflict);
            }

            return Result.Accepted(ToCreatedDto(concurrentJob, requestId));
        }

        await _auditLogService.LogAsync(
            "EnqueueAiJob",
            nameof(AiJob),
            job.Id.ToString(),
            new { job.JobType, job.ProjectId, job.SchemaId, job.RequestHash, job.IdempotencyKey, requestId },
            ct);

        return Result.Accepted(ToCreatedDto(job, requestId));
    }

    public async Task<Result<IReadOnlyList<AiJobSummaryDto>>> ListJobsAsync(
        Guid? projectId,
        string? status,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<IReadOnlyList<AiJobSummaryDto>>();
        }

        var query = _aiJobRepo.GetQueryable()
            .AsNoTracking()
            .Include(job => job.Project)
                .ThenInclude(project => project!.Organization)
            .Include(job => job.Drafts)
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(job => job.ProjectId == projectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(job => job.Status == normalizedStatus);
        }

        if (!IsAdmin())
        {
            var projectIds = await _projectMemberRepo.GetQueryable()
                .Where(member => member.UserId == currentUserId.Value)
                .Select(member => member.ProjectId)
                .ToListAsync(ct);
            var organizationIds = await _organizationMemberRepo.GetQueryable()
                .Where(member => member.UserId == currentUserId.Value)
                .Select(member => member.OrganizationId)
                .ToListAsync(ct);

            query = query.Where(job =>
                job.RequestedById == currentUserId.Value ||
                (job.ProjectId.HasValue && projectIds.Contains(job.ProjectId.Value)) ||
                (job.TenantId.HasValue && organizationIds.Contains(job.TenantId.Value)) ||
                (job.Project != null && job.Project.OwnerId == currentUserId.Value) ||
                (job.Project != null && job.Project.Organization != null && job.Project.Organization.OwnerId == currentUserId.Value));
        }

        var jobs = await query
            .OrderByDescending(job => job.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<AiJobSummaryDto>>(jobs.Select(ToJobSummaryDto).ToList());
    }

    public async Task<Result<AiJobDetailDto>> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var access = await GetVisibleJobAsync(jobId, ct);
        return access.IsSuccess
            ? Result.Success(ToJobDetailDto(access.Data!))
            : Result.Failure<AiJobDetailDto>(access.Error!, access.StatusCode, access.ErrorCode);
    }

    public async Task<Result<AiJobResultDto>> GetJobResultAsync(Guid jobId, CancellationToken ct = default)
    {
        var access = await GetVisibleJobAsync(jobId, ct);
        if (!access.IsSuccess)
        {
            return Result.Failure<AiJobResultDto>(access.Error!, access.StatusCode, access.ErrorCode);
        }

        var job = access.Data!;
        if (!string.Equals(job.Status, AiJobStatuses.Succeeded, StringComparison.Ordinal))
        {
            return Result.Failure<AiJobResultDto>(
                "The job result is not available.",
                AiJobStatuses.IsTerminal(job.Status) ? 409 : 202,
                job.LastErrorCode);
        }

        if (string.IsNullOrWhiteSpace(job.ResultJson))
        {
            return Result.Failure<AiJobResultDto>(
                "The job succeeded without a persisted result.",
                500,
                AiErrorCodes.SchemaInvalid);
        }

        JsonElement resultJson;
        try
        {
            resultJson = ParseJson(job.ResultJson);
        }
        catch (JsonException)
        {
            return Result.Failure<AiJobResultDto>(
                "The persisted result is invalid JSON.",
                500,
                AiErrorCodes.SchemaInvalid);
        }

        var usageId = job.UsageEntries
            .OrderByDescending(entry => entry.CreatedAt)
            .Select(entry => (Guid?)entry.Id)
            .FirstOrDefault();

        return Result.Success(new AiJobResultDto(
            job.Id,
            job.SchemaId,
            job.SchemaVersion,
            resultJson,
            job.ResultHash,
            job.Drafts.Select(draft => draft.Id).ToList(),
            job.Sources.OrderBy(source => source.SortOrder).Select(ToSourceDto).ToList(),
            usageId,
            job.CacheHit,
            job.IsMock,
            job.MockReason));
    }

    public async Task<Result<AiJobDetailDto>> RetryJobAsync(
        Guid jobId,
        RetryAiJobDto dto,
        CancellationToken ct = default)
    {
        var access = await GetVisibleJobAsync(jobId, ct, tracking: true);
        if (!access.IsSuccess)
        {
            return Result.Failure<AiJobDetailDto>(access.Error!, access.StatusCode, access.ErrorCode);
        }

        if (_platformOptions != null && !_platformOptions.CurrentValue.Enabled)
        {
            return Result.Failure<AiJobDetailDto>(
                "The canonical AI job platform is disabled.",
                503,
                AiErrorCodes.PlatformDisabled);
        }

        var job = access.Data!;
        if (string.Equals(job.Status, AiJobStatuses.Retrying, StringComparison.Ordinal) ||
            string.Equals(job.Status, AiJobStatuses.Queued, StringComparison.Ordinal))
        {
            return Result.Success(ToJobDetailDto(job));
        }

        if (job.Status is not (AiJobStatuses.Failed or AiJobStatuses.Canceled) || job.AttemptCount >= job.MaxAttempts)
        {
            return Result.Failure<AiJobDetailDto>(
                "The job cannot be retried.",
                409,
                AiErrorCodes.JobNotRetryable);
        }

        var currentUserId = _currentUserService.UserId!.Value;
        if (!string.IsNullOrWhiteSpace(dto.ProviderOverride) &&
            (job.Project == null || !await CanManageProjectAsync(job.Project, currentUserId, ct)))
        {
            return Result.Failure<AiJobDetailDto>(
                "Provider override requires project management permission.",
                403,
                AiErrorCodes.PermissionDenied);
        }

        var retryPrivacyDecision = await EvaluateJobPrivacyAsync(
            job,
            dto.ProviderOverride ?? job.ProviderHint,
            ct);
        if (retryPrivacyDecision?.Allowed == false)
        {
            return Result.Failure<AiJobDetailDto>(
                retryPrivacyDecision.Reason,
                403,
                IsConsentError(retryPrivacyDecision.ErrorCode)
                    ? AiErrorCodes.ConsentRequired
                    : AiErrorCodes.SensitiveBlocked);
        }

        if (_costService != null && !await _costService.EnsureBudgetAvailableAsync(job.TenantId, job.ProjectId, ct))
        {
            return Result.Failure<AiJobDetailDto>(
                "The effective AI budget has been exceeded.",
                429,
                AiErrorCodes.BudgetExceeded);
        }

        if (_sourceGuard != null && job.ProjectId.HasValue)
        {
            var sourceValidation = await _sourceGuard.ValidateAsync(
                job.ProjectId.Value,
                currentUserId,
                job.Sources.Select(ToSourceInputDto).ToList(),
                enforceFreshness: true,
                ct);
            if (!sourceValidation.IsAllowed)
            {
                return Result.Failure<AiJobDetailDto>(
                    sourceValidation.ErrorMessage ?? "The AI source is no longer available.",
                    sourceValidation.ErrorCode == AiErrorCodes.SourceStale ? 409 : 403,
                    sourceValidation.ErrorCode);
            }
        }

        var now = DateTimeOffset.UtcNow;
        job.Status = AiJobStatuses.Retrying;
        job.AvailableAt = now;
        job.NextRetryAt = now;
        job.FinishedAt = null;
        job.CanceledAt = null;
        job.CanceledById = null;
        job.CancellationRequestedAt = null;
        job.LastErrorCode = null;
        job.LastErrorMessage = null;
        job.LastErrorRetryable = false;
        if (!string.IsNullOrWhiteSpace(dto.ProviderOverride))
        {
            job.ProviderHint = dto.ProviderOverride.Trim();
        }

        var dispatch = job.Dispatch;
        if (dispatch == null)
        {
            dispatch = new AiJobDispatch { AiJobId = job.Id };
            await RequireDispatchRepository().AddAsync(dispatch, ct);
            job.Dispatch = dispatch;
        }
        dispatch.AvailableAt = now;
        dispatch.LeaseOwner = null;
        dispatch.LeaseExpiresAt = null;
        dispatch.CompletedAt = null;
        dispatch.LastDispatchErrorCode = null;
        dispatch.LastDispatchError = null;

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("RetryAiJob", nameof(AiJob), job.Id.ToString(), new { job.AttemptCount, job.ProviderHint }, ct);
        return Result.Success(ToJobDetailDto(job));
    }

    public async Task<Result<AiJobDetailDto>> CancelJobAsync(
        Guid jobId,
        CancelAiJobDto dto,
        CancellationToken ct = default)
    {
        var access = await GetVisibleJobAsync(jobId, ct, tracking: true);
        if (!access.IsSuccess)
        {
            return Result.Failure<AiJobDetailDto>(access.Error!, access.StatusCode, access.ErrorCode);
        }

        var job = access.Data!;
        if (string.Equals(job.Status, AiJobStatuses.Canceled, StringComparison.Ordinal))
        {
            return Result.Success(ToJobDetailDto(job));
        }

        if (AiJobStatuses.IsTerminal(job.Status))
        {
            return Result.Failure<AiJobDetailDto>(
                "The job is already terminal.",
                409,
                AiErrorCodes.JobNotCancelable);
        }

        var now = DateTimeOffset.UtcNow;
        job.CancellationRequestedAt = now;
        job.CanceledAt = now;
        job.CanceledById = _currentUserService.UserId;
        job.FinishedAt = now;
        job.Status = AiJobStatuses.Canceled;
        job.ProgressPercent = Math.Min(job.ProgressPercent, 99);
        job.LastErrorCode = null;
        job.LastErrorMessage = NormalizeOptional(dto.Reason);

        if (job.Dispatch != null)
        {
            job.Dispatch.CompletedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("CancelAiJob", nameof(AiJob), job.Id.ToString(), new { dto.Reason }, ct);
        return Result.Success(ToJobDetailDto(job));
    }

    public async Task<Result<IReadOnlyList<AiDraftSummaryDto>>> ListDraftsAsync(
        Guid? projectId,
        string? type,
        string? status,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<IReadOnlyList<AiDraftSummaryDto>>();
        }

        var query = _aiDraftRepo.GetQueryable()
            .AsNoTracking()
            .Include(draft => draft.Project)
                .ThenInclude(project => project.Organization)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(draft => draft.ProjectId == projectId.Value);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(draft => draft.DraftType == type.Trim());
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(draft => draft.Status == normalized);
        }

        var candidates = await query.OrderByDescending(draft => draft.CreatedAt).Take(200).ToListAsync(ct);
        var visible = new List<AiDraftSummaryDto>();
        foreach (var draft in candidates)
        {
            if (await CanAccessProjectAsync(draft.Project, currentUserId.Value, ct))
            {
                visible.Add(ToDraftSummaryDto(draft));
            }
        }

        return Result.Success<IReadOnlyList<AiDraftSummaryDto>>(visible);
    }

    public async Task<Result<AiDraftDetailDto>> GetDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var access = await GetVisibleDraftAsync(draftId, tracking: false, ct);
        return access.IsSuccess
            ? Result.Success(ToDraftDetailDto(access.Data!))
            : Result.Failure<AiDraftDetailDto>(access.Error!, access.StatusCode, access.ErrorCode);
    }

    public async Task<Result<AiDraftDetailDto>> PatchDraftAsync(
        Guid draftId,
        PatchAiDraftDto dto,
        CancellationToken ct = default)
    {
        var access = await GetVisibleDraftAsync(draftId, tracking: true, ct);
        if (!access.IsSuccess)
        {
            return Result.Failure<AiDraftDetailDto>(access.Error!, access.StatusCode, access.ErrorCode);
        }

        var draft = access.Data!;
        if (!string.Equals(draft.Status, AiDraftStatuses.PendingReview, StringComparison.Ordinal))
        {
            return Result.Failure<AiDraftDetailDto>(
                "Only pending drafts can be edited.",
                409,
                AiErrorCodes.DraftAlreadyConfirmed);
        }

        if (!MatchesRowVersion(draft.RowVersion, dto.RowVersion))
        {
            return Result.Failure<AiDraftDetailDto>(
                "The draft was modified by another request.",
                409,
                AiErrorCodes.DraftConcurrencyConflict);
        }

        try
        {
            JsonDocument.Parse(dto.WorkingPayloadJson).Dispose();
            if (IsTaskDraft(draft.DraftType))
            {
                _ = DeserializeTaskDraftPayload(dto.WorkingPayloadJson, draft.DraftType);
            }
        }
        catch (JsonException)
        {
            return Result.Failure<AiDraftDetailDto>(
                "working_payload_json is invalid for this draft schema.",
                422,
                AiErrorCodes.SchemaInvalid);
        }

        draft.WorkingPayloadJson = dto.WorkingPayloadJson.Trim();
        draft.PayloadJson = draft.WorkingPayloadJson;
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("EditAiDraft", nameof(AiGeneratedDraft), draft.Id.ToString(), new { draft.AiJobId }, ct);
        return Result.Success(ToDraftDetailDto(draft));
    }

    public async Task<Result<AiDraftDetailDto>> RejectDraftAsync(
        Guid draftId,
        RejectAiDraftDto dto,
        CancellationToken ct = default)
    {
        var access = await GetVisibleDraftAsync(draftId, tracking: true, ct);
        if (!access.IsSuccess)
        {
            return Result.Failure<AiDraftDetailDto>(access.Error!, access.StatusCode, access.ErrorCode);
        }

        var draft = access.Data!;
        if (string.Equals(draft.Status, AiDraftStatuses.Rejected, StringComparison.Ordinal) &&
            string.Equals(draft.ConfirmationIdempotencyKey, dto.IdempotencyKey, StringComparison.Ordinal))
        {
            return Result.Success(ToDraftDetailDto(draft));
        }

        if (!string.Equals(draft.Status, AiDraftStatuses.PendingReview, StringComparison.Ordinal))
        {
            return Result.Failure<AiDraftDetailDto>(
                "Only pending drafts can be rejected.",
                409,
                AiErrorCodes.DraftAlreadyConfirmed);
        }

        if (!MatchesRowVersion(draft.RowVersion, dto.RowVersion))
        {
            return Result.Failure<AiDraftDetailDto>(
                "The draft was modified by another request.",
                409,
                AiErrorCodes.DraftConcurrencyConflict);
        }

        var now = DateTimeOffset.UtcNow;
        draft.Status = AiDraftStatuses.Rejected;
        draft.RejectedById = _currentUserService.UserId;
        draft.RejectedAt = now;
        draft.RejectionReason = dto.Reason.Trim();
        draft.ConfirmationIdempotencyKey = NormalizeOptional(dto.IdempotencyKey);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("RejectAiDraft", nameof(AiGeneratedDraft), draft.Id.ToString(), new { dto.Reason }, ct);
        return Result.Success(ToDraftDetailDto(draft));
    }

    public async Task<Result<AiDraftConfirmResultDto>> ConfirmDraftAsync(Guid draftId, ConfirmAiDraftDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<AiDraftConfirmResultDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.ConfirmAction))
        {
            return Result.Failure<AiDraftConfirmResultDto>("confirm_action is required.", 400);
        }

        var draft = await _aiDraftRepo.GetQueryable()
            .Include(item => item.Project)
                .ThenInclude(project => project.Organization)
            .Include(item => item.AiJob)
            .FirstOrDefaultAsync(item => item.Id == draftId, ct);
        if (draft == null)
        {
            return Result.Failure<AiDraftConfirmResultDto>("Draft was not found.", 404);
        }

        if (!await CanAccessProjectAsync(draft.Project, currentUserId.Value, ct))
        {
            return Result.Forbidden<AiDraftConfirmResultDto>();
        }

        var confirmationKey = NormalizeOptional(dto.IdempotencyKey);
        if (confirmationKey == null)
        {
            return Result.Failure<AiDraftConfirmResultDto>(
                "Idempotency-Key is required for draft confirmation.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        if (string.Equals(draft.Status, AiDraftStatuses.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            if (confirmationKey != null &&
                string.Equals(draft.ConfirmationIdempotencyKey, confirmationKey, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(draft.ConfirmationResultJson))
            {
                var completed = JsonSerializer.Deserialize<AiDraftConfirmResultDto>(draft.ConfirmationResultJson, JsonOptions);
                if (completed != null) return Result.Success(completed);
            }

            return Result.Failure<AiDraftConfirmResultDto>(
                "Draft was already confirmed.",
                409,
                AiErrorCodes.DraftAlreadyConfirmed);
        }

        if (!string.IsNullOrWhiteSpace(draft.ConfirmationIdempotencyKey))
        {
            return Result.Failure<AiDraftConfirmResultDto>(
                string.Equals(draft.ConfirmationIdempotencyKey, confirmationKey, StringComparison.Ordinal)
                    ? "Draft confirmation is already in progress and requires reconciliation before retry."
                    : "Draft confirmation is already claimed by another request.",
                409,
                AiErrorCodes.DraftConfirmationInProgress);
        }

        if (draft.Status is AiDraftStatuses.Rejected or AiDraftStatuses.Expired)
        {
            return Result.Failure<AiDraftConfirmResultDto>(
                "The draft is no longer confirmable.",
                409,
                AiErrorCodes.DraftAlreadyConfirmed);
        }

        if (!string.IsNullOrWhiteSpace(dto.RowVersion) && !MatchesRowVersion(draft.RowVersion, dto.RowVersion))
        {
            return Result.Failure<AiDraftConfirmResultDto>(
                "The draft was modified by another request.",
                409,
                AiErrorCodes.DraftConcurrencyConflict);
        }

        if (draft.ExpiresAt.HasValue && draft.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            draft.Status = AiDraftStatuses.Expired;
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<AiDraftConfirmResultDto>(
                "The draft has expired.",
                409,
                AiErrorCodes.DraftAlreadyConfirmed);
        }

        if (draft.AiJob.Status is AiJobStatuses.Failed or AiJobStatuses.Canceled)
        {
            return Result.Failure<AiDraftConfirmResultDto>(
                "A failed or canceled job result cannot be confirmed.",
                409,
                AiErrorCodes.JobNotRetryable);
        }

        var confirmationPrivacyDecision = await EvaluateJobPrivacyAsync(draft.AiJob, draft.AiJob.ProviderHint, ct);
        if (confirmationPrivacyDecision?.Allowed == false)
        {
            return Result.Failure<AiDraftConfirmResultDto>(
                confirmationPrivacyDecision.Reason,
                403,
                IsConsentError(confirmationPrivacyDecision.ErrorCode)
                    ? AiErrorCodes.ConsentRequired
                    : AiErrorCodes.SensitiveBlocked);
        }

        if (_sourceGuard != null)
        {
            var sourceValidation = await _sourceGuard.ValidateAsync(
                draft.ProjectId,
                currentUserId.Value,
                draft.AiJob.Sources.Select(ToSourceInputDto).ToList(),
                enforceFreshness: true,
                ct);
            if (!sourceValidation.IsAllowed)
            {
                return Result.Failure<AiDraftConfirmResultDto>(
                    sourceValidation.ErrorMessage ?? "The AI source is no longer available.",
                    sourceValidation.ErrorCode == AiErrorCodes.SourceStale ? 409 : 403,
                    sourceValidation.ErrorCode);
            }
        }

        var payloadJson = string.IsNullOrWhiteSpace(dto.EditedPayloadJson)
            ? draft.WorkingPayloadJson
            : dto.EditedPayloadJson.Trim();
        AiTaskDraftPayload? payload = null;
        if (string.Equals(draft.DraftType, "MeetingActionItems", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(draft.DraftType, "TaskDraft", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                payload = DeserializeTaskDraftPayload(payloadJson, draft.DraftType);
            }
            catch (JsonException)
            {
                return Result.Failure<AiDraftConfirmResultDto>("edited_payload is invalid JSON.", 400);
            }
        }

        var createdTaskIds = new List<Guid>();
        var normalizedAction = dto.ConfirmAction.Trim();

        draft.ConfirmationIdempotencyKey = confirmationKey;
        draft.ConfirmAction = normalizedAction;
        draft.ConfirmationNote = NormalizeOptional(dto.ConfirmationNote);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AiDraftConfirmResultDto>(
                "The draft was claimed by another confirmation request.",
                409,
                AiErrorCodes.DraftConcurrencyConflict);
        }

        if (string.Equals(normalizedAction, "reject", StringComparison.OrdinalIgnoreCase))
        {
            draft.Status = AiDraftStatuses.Rejected;
            draft.RejectedById = currentUserId.Value;
            draft.RejectedAt = DateTimeOffset.UtcNow;
            draft.RejectionReason = string.IsNullOrWhiteSpace(dto.ConfirmationNote) ? "Rejected during review." : dto.ConfirmationNote.Trim();
            draft.ConfirmAction = normalizedAction;
            draft.ConfirmationNote = string.IsNullOrWhiteSpace(dto.ConfirmationNote) ? null : dto.ConfirmationNote.Trim();
            draft.ConfirmationIdempotencyKey = confirmationKey;

            await _aiDraftRepo.UpdateAsync(draft, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (_complianceService != null)
            {
                await _complianceService.LogAuditEventAsync(
                    draft.Project.OrganizationId,
                    draft.ProjectId,
                    currentUserId.Value,
                    "AI_TOOL_REJECTED",
                    "AiGeneratedDraft",
                    null,
                    draft.PayloadJson,
                    null,
                    ct
                );
            }

            await _auditLogService.LogAsync(
                "RejectAiDraft",
                nameof(AiGeneratedDraft),
                draft.Id.ToString(),
                new { draft.ProjectId, draft.ConfirmAction },
                ct);

            return Result.Success(new AiDraftConfirmResultDto(
                draft.Id,
                draft.Status,
                normalizedAction,
                0,
                Array.Empty<Guid>()));
        }

        if (string.Equals(normalizedAction, "execute_action", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(draft.DraftType, "CreateTask", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                
                string title = root.GetProperty("title").GetString() ?? string.Empty;
                string? description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
                string priority = root.TryGetProperty("priority", out var prioProp) ? prioProp.GetString() ?? "Medium" : "Medium";
                Guid? assigneeId = null;
                if (root.TryGetProperty("assigneeId", out var assProp) && assProp.ValueKind == JsonValueKind.String && Guid.TryParse(assProp.GetString(), out var parsedAssignee))
                {
                    assigneeId = parsedAssignee;
                }
                DateTimeOffset? dueDate = null;
                if (root.TryGetProperty("dueDate", out var dueProp) && dueProp.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(dueProp.GetString(), out var parsedDue))
                {
                    dueDate = parsedDue;
                }

                if (_taskService != null)
                {
                    var taskDto = new Qaly.Application.DTOs.Task.CreateTaskDto(title, description, priority, dueDate, null, draft.ProjectId, assigneeId);
                    var taskResult = await _taskService.CreateAsync(taskDto, ct);
                    if (!taskResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute CreateTask: {taskResult.Error}", taskResult.StatusCode);
                    }
                    createdTaskIds.Add(taskResult.Data!.Id);
                }
                else
                {
                    var task = new TaskItem
                    {
                        Title = title,
                        Description = description,
                        Priority = priority,
                        Status = "Todo",
                        DueDate = dueDate,
                        ProjectId = draft.ProjectId,
                        ReporterId = currentUserId.Value,
                        AssigneeId = assigneeId
                    };
                    await _taskRepo.AddAsync(task, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    createdTaskIds.Add(task.Id);
                }
            }
            else if (string.Equals(draft.DraftType, "UpdateTaskStatus", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                string status = root.GetProperty("status").GetString()!;

                if (_taskService != null)
                {
                    var taskResult = await _taskService.UpdateStatusAsync(taskId, status, ct: ct);
                    if (!taskResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute UpdateTaskStatus: {taskResult.Error}", taskResult.StatusCode);
                    }
                }
                else
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        task.Status = status;
                        await _taskRepo.UpdateAsync(task, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "AssignTask", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                Guid assigneeId = Guid.Parse(root.GetProperty("assigneeId").GetString()!);

                if (_taskService != null)
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        var taskDto = new Qaly.Application.DTOs.Task.UpdateTaskDto(task.Title, task.Description, task.Status, task.Priority, task.DueDate, task.EstimatedHours, task.ActualHours, assigneeId, task.IsPrivate);
                        var taskResult = await _taskService.UpdateAsync(taskId, taskDto, ct);
                        if (!taskResult.IsSuccess)
                        {
                            return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute AssignTask: {taskResult.Error}", taskResult.StatusCode);
                        }
                    }
                }
                else
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        task.AssigneeId = assigneeId;
                        await _taskRepo.UpdateAsync(task, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "SetTaskPriority", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                string priority = root.GetProperty("priority").GetString()!;

                if (_taskService != null)
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        var taskDto = new Qaly.Application.DTOs.Task.UpdateTaskDto(task.Title, task.Description, task.Status, priority, task.DueDate, task.EstimatedHours, task.ActualHours, task.AssigneeId, task.IsPrivate);
                        var taskResult = await _taskService.UpdateAsync(taskId, taskDto, ct);
                        if (!taskResult.IsSuccess)
                        {
                            return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute SetTaskPriority: {taskResult.Error}", taskResult.StatusCode);
                        }
                    }
                }
                else
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        task.Priority = priority;
                        await _taskRepo.UpdateAsync(task, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "AddDueDate", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                DateTimeOffset dueDate = DateTimeOffset.Parse(root.GetProperty("dueDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

                if (_taskService != null)
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        var taskDto = new Qaly.Application.DTOs.Task.UpdateTaskDto(task.Title, task.Description, task.Status, task.Priority, dueDate, task.EstimatedHours, task.ActualHours, task.AssigneeId, task.IsPrivate);
                        var taskResult = await _taskService.UpdateAsync(taskId, taskDto, ct);
                        if (!taskResult.IsSuccess)
                        {
                            return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute AddDueDate: {taskResult.Error}", taskResult.StatusCode);
                        }
                    }
                }
                else
                {
                    var task = await _taskRepo.GetByIdAsync(taskId, ct);
                    if (task != null)
                    {
                        task.DueDate = dueDate;
                        await _taskRepo.UpdateAsync(task, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "AddComment", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);
                string content = root.GetProperty("content").GetString()!;

                if (_commentService != null)
                {
                    var commentDto = new Qaly.Application.DTOs.Comment.CreateCommentDto(content, taskId);
                    var commentResult = await _commentService.CreateAsync(commentDto, ct);
                    if (!commentResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute AddComment: {commentResult.Error}", commentResult.StatusCode);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "StartTimeTracking", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid taskId = Guid.Parse(root.GetProperty("taskId").GetString()!);

                if (_timeTrackingService != null)
                {
                    var ttResult = await _timeTrackingService.StartTimerAsync(taskId, ct);
                    if (!ttResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute StartTimeTracking: {ttResult.Error}", ttResult.StatusCode);
                    }
                }
            }
            else if (string.Equals(draft.DraftType, "StopTimeTracking", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;
                Guid entryId = Guid.Parse(root.GetProperty("entryId").GetString()!);

                if (_timeTrackingService != null)
                {
                    var ttResult = await _timeTrackingService.StopTimerAsync(entryId, ct);
                    if (!ttResult.IsSuccess)
                    {
                        return Result.Failure<AiDraftConfirmResultDto>($"Failed to execute StopTimeTracking: {ttResult.Error}", ttResult.StatusCode);
                    }
                }
            }
        }
        else if (string.Equals(normalizedAction, "create_tasks", StringComparison.OrdinalIgnoreCase))
        {
            if (!await CanManageProjectAsync(draft.Project, currentUserId.Value, ct))
            {
                return Result.Failure<AiDraftConfirmResultDto>("Access denied for create_tasks confirm action.", 403);
            }

            if (payload == null)
            {
                return Result.Failure<AiDraftConfirmResultDto>("Invalid draft payload.", 400);
            }

            MeetingImport? meetingImport = null;
            Dictionary<int, MeetingActionItemMapping>? existingMappingsByIndex = null;
            MeetingExtractionPayload? meetingExtraction = null;

            if (string.Equals(draft.DraftType, "MeetingActionItems", StringComparison.OrdinalIgnoreCase))
            {
                meetingImport = await _meetingImportRepo.GetQueryable()
                    .FirstOrDefaultAsync(item => item.AiDraftId == draft.Id, ct);
                if (meetingImport != null)
                {
                    existingMappingsByIndex = await _meetingActionItemMappingRepo.GetQueryable()
                        .Where(mapping => mapping.MeetingImportId == meetingImport.Id)
                        .ToDictionaryAsync(mapping => mapping.ActionItemIndex, ct);
                    meetingExtraction = TryDeserializeMeetingExtractionPayload(payloadJson);
                }
            }

            for (var itemIndex = 0; itemIndex < payload.Tasks.Count; itemIndex++)
            {
                var item = payload.Tasks[itemIndex];
                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    continue;
                }

                if (existingMappingsByIndex != null &&
                    existingMappingsByIndex.TryGetValue(itemIndex, out var existingMapping) &&
                    existingMapping.TaskId.HasValue)
                {
                    continue;
                }

                var normalizedPriority = TaskStatusRules.IsValidPriority(item.Priority)
                    ? TaskStatusRules.NormalizePriority(item.Priority)
                    : "Medium";
                var normalizedStatus = TaskStatusRules.IsValidStatus(item.Status)
                    ? TaskStatusRules.NormalizeStatus(item.Status)
                    : "Todo";

                Guid? validAssigneeId = null;
                if (item.AssigneeId.HasValue && item.AssigneeId.Value != Guid.Empty)
                {
                    var isValidAssignee = await _userRepo.GetQueryable()
                        .AnyAsync(user => user.Id == item.AssigneeId.Value && user.IsActive, ct) &&
                        await IsProjectUserAsync(draft.Project, item.AssigneeId.Value, ct);
                    if (isValidAssignee)
                    {
                        validAssigneeId = item.AssigneeId.Value;
                    }
                }

                var task = new TaskItem
                {
                    Title = item.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                    Priority = normalizedPriority,
                    Status = normalizedStatus,
                    DueDate = item.DueDate,
                    ProjectId = draft.ProjectId,
                    ReporterId = currentUserId.Value,
                    AssigneeId = validAssigneeId
                };

                await _taskRepo.AddAsync(task, ct);
                createdTaskIds.Add(task.Id);

                if (existingMappingsByIndex != null && meetingImport != null)
                {
                    var sourceActionItem = (meetingExtraction != null && itemIndex >= 0 && itemIndex < meetingExtraction.ActionItems.Count)
                        ? meetingExtraction.ActionItems[itemIndex]
                        : null;

                    if (existingMappingsByIndex.TryGetValue(itemIndex, out var existingMappingWithoutTask))
                    {
                        existingMappingWithoutTask.TaskId = task.Id;
                        existingMappingWithoutTask.Status = "Linked";
                        existingMappingWithoutTask.SourceTitle = sourceActionItem?.Title ?? item.Title.Trim();
                        existingMappingWithoutTask.SourcePriority = sourceActionItem?.Priority ?? normalizedPriority;
                        existingMappingWithoutTask.SourceDueDate = sourceActionItem?.DueDate ?? item.DueDate;
                        existingMappingWithoutTask.SourceQuote = sourceActionItem?.SourceEvidence ?? sourceActionItem?.Description ?? item.Description;
                        existingMappingWithoutTask.CreatedById = currentUserId.Value;
                        await _meetingActionItemMappingRepo.UpdateAsync(existingMappingWithoutTask, ct);
                    }
                    else
                    {
                        var mapping = new MeetingActionItemMapping
                        {
                            MeetingImportId = meetingImport.Id,
                            ActionItemIndex = itemIndex,
                            TaskId = task.Id,
                            Status = "Linked",
                            SourceTitle = sourceActionItem?.Title ?? item.Title.Trim(),
                            SourcePriority = sourceActionItem?.Priority ?? normalizedPriority,
                            SourceDueDate = sourceActionItem?.DueDate ?? item.DueDate,
                            SourceQuote = sourceActionItem?.SourceEvidence ?? sourceActionItem?.Description ?? item.Description,
                            CreatedById = currentUserId.Value
                        };

                        await _meetingActionItemMappingRepo.AddAsync(mapping, ct);
                        existingMappingsByIndex[itemIndex] = mapping;
                    }
                }

                if (validAssigneeId.HasValue)
                {
                    await _taskAssignmentRepo.AddAsync(new TaskAssignment
                    {
                        TaskItemId = task.Id,
                        UserId = validAssigneeId.Value,
                        AssignedAt = DateTimeOffset.UtcNow,
                        AssignedByUserId = currentUserId
                    }, ct);
                }
            }
        }

        draft.PayloadJson = payloadJson;
        draft.WorkingPayloadJson = payloadJson;
        draft.Status = AiDraftStatuses.Confirmed;
        draft.ConfirmedById = currentUserId.Value;
        draft.ConfirmedAt = DateTimeOffset.UtcNow;
        draft.ConfirmAction = normalizedAction;
        draft.ConfirmationNote = string.IsNullOrWhiteSpace(dto.ConfirmationNote) ? null : dto.ConfirmationNote.Trim();
        draft.ConfirmationIdempotencyKey = confirmationKey;

        var confirmationResult = new AiDraftConfirmResultDto(
            draft.Id,
            draft.Status,
            normalizedAction,
            createdTaskIds.Count,
            createdTaskIds);
        draft.ConfirmationResultJson = JsonSerializer.Serialize(confirmationResult, JsonOptions);

        await _aiDraftRepo.UpdateAsync(draft, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        if (_complianceService != null)
        {
            await _complianceService.LogAuditEventAsync(
                draft.Project.OrganizationId,
                draft.ProjectId,
                currentUserId.Value,
                "AI_DRAFT_CONFIRMED",
                "AiGeneratedDraft",
                null,
                null,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    draft.Id,
                    draft.DraftType,
                    draft.PayloadJson,
                    draft.Status
                }),
                ct
            );

            await _complianceService.LogAuditEventAsync(
                draft.Project.OrganizationId,
                draft.ProjectId,
                currentUserId.Value,
                "AI_TOOL_EXECUTED",
                draft.DraftType,
                null,
                draft.PayloadJson,
                null,
                ct
            );
        }

        await _auditLogService.LogAsync(
            "ConfirmAiDraft",
            nameof(AiGeneratedDraft),
            draft.Id.ToString(),
            new
            {
                draft.ProjectId,
                draft.ConfirmAction,
                createdTaskIds.Count
            },
            ct);

        return Result.Success(confirmationResult);
    }

    private async Task<Result<AiJob>> GetVisibleJobAsync(
        Guid jobId,
        CancellationToken ct,
        bool tracking = false)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure<AiJob>("Authentication is required.", 403, AiErrorCodes.PermissionDenied);
        }

        var query = _aiJobRepo.GetQueryable()
            .Include(job => job.Project)
                .ThenInclude(project => project!.Organization)
            .Include(job => job.Dispatch)
            .Include(job => job.Sources)
            .Include(job => job.Drafts)
            .Include(job => job.ProviderAttempts)
            .Include(job => job.UsageEntries)
            .AsQueryable();
        if (!tracking) query = query.AsNoTracking();

        var job = await query.FirstOrDefaultAsync(item => item.Id == jobId, ct);
        if (job == null || !await CanAccessJobAsync(job, currentUserId.Value, ct))
        {
            return Result.Failure<AiJob>("AI job was not found.", 404, AiErrorCodes.JobNotFound);
        }

        return Result.Success(job);
    }

    private async Task<Result<AiGeneratedDraft>> GetVisibleDraftAsync(
        Guid draftId,
        bool tracking,
        CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Failure<AiGeneratedDraft>("Authentication is required.", 403, AiErrorCodes.PermissionDenied);
        }

        var query = _aiDraftRepo.GetQueryable()
            .Include(draft => draft.Project)
                .ThenInclude(project => project.Organization)
            .Include(draft => draft.AiJob)
                .ThenInclude(job => job.Sources)
            .AsQueryable();
        if (!tracking) query = query.AsNoTracking();

        var draft = await query.FirstOrDefaultAsync(item => item.Id == draftId, ct);
        if (draft == null || !await CanAccessProjectAsync(draft.Project, currentUserId.Value, ct))
        {
            return Result.Failure<AiGeneratedDraft>("AI draft was not found.", 404, AiErrorCodes.JobNotFound);
        }

        return Result.Success(draft);
    }

    private async Task<bool> CanAccessJobAsync(AiJob job, Guid currentUserId, CancellationToken ct)
    {
        if (IsAdmin() || job.RequestedById == currentUserId) return true;
        if (job.Project != null && await CanAccessProjectAsync(job.Project, currentUserId, ct)) return true;
        if (!job.TenantId.HasValue) return false;

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == job.TenantId.Value && member.UserId == currentUserId, ct);
    }

    private static AiJobCreatedDto ToCreatedDto(AiJob job, string? requestId)
        => new(
            job.Id,
            job.Status,
            job.EstimatedCostUsd,
            job.CacheKey,
            job.Drafts.OrderBy(draft => draft.CreatedAt).Select(draft => (Guid?)draft.Id).FirstOrDefault(),
            $"/api/ai/jobs/{job.Id}",
            $"/api/ai/jobs/{job.Id}/result",
            requestId);

    private static AiJobSummaryDto ToJobSummaryDto(AiJob job)
        => new(
            job.Id,
            job.JobType,
            job.ProjectId,
            job.Status,
            job.ProgressPercent,
            job.AttemptCount,
            job.MaxAttempts,
            job.CreatedAt,
            job.StartedAt,
            job.FinishedAt,
            job.LastErrorCode,
            job.IsMock,
            job.Drafts.Select(draft => draft.Id).ToList());

    private static AiJobDetailDto ToJobDetailDto(AiJob job)
        => new(
            job.Id,
            job.JobType,
            job.TenantId,
            job.ProjectId,
            job.RequestedById,
            job.Status,
            job.ProgressPercent,
            job.SchemaId,
            job.SchemaVersion,
            job.Sensitive,
            job.CloudEligible,
            job.AttemptCount,
            job.MaxAttempts,
            job.CreatedAt,
            job.AvailableAt,
            job.StartedAt,
            job.FinishedAt,
            job.CanceledAt,
            job.LastErrorCode,
            job.LastErrorMessage,
            job.LastErrorRetryable,
            job.SelectedProvider,
            job.SelectedModel,
            job.EstimatedCostUsd,
            job.ActualCostUsd,
            job.CacheHit,
            job.IsMock,
            job.MockReason,
            job.Sources.OrderBy(source => source.SortOrder).Select(ToSourceDto).ToList(),
            job.Drafts.Select(draft => draft.Id).ToList(),
            EncodeRowVersion(job.RowVersion));

    private static AiJobSourceDto ToSourceDto(AiJobSource source)
        => new(
            source.SourceType,
            source.SourceEntityId,
            source.LegacySourceKey,
            source.SourceVersion,
            source.SourceHash,
            source.SourceTimestamp);

    private static AiJobSourceInputDto ToSourceInputDto(AiJobSource source)
        => new(
            source.SourceType,
            source.SourceEntityId,
            source.LegacySourceKey,
            source.SourceVersion,
            source.SourceHash,
            source.SourceTimestamp);

    private static AiDraftSummaryDto ToDraftSummaryDto(AiGeneratedDraft draft)
        => new(
            draft.Id,
            draft.AiJobId,
            draft.ProjectId,
            draft.DraftType,
            draft.Status,
            draft.Confidence,
            draft.CreatedAt,
            draft.ExpiresAt);

    private static AiDraftDetailDto ToDraftDetailDto(AiGeneratedDraft draft)
        => new(
            draft.Id,
            draft.AiJobId,
            draft.ProjectId,
            draft.DraftType,
            draft.Status,
            ParseJson(string.IsNullOrWhiteSpace(draft.OriginalPayloadJson) ? draft.PayloadJson : draft.OriginalPayloadJson),
            ParseJson(string.IsNullOrWhiteSpace(draft.WorkingPayloadJson) ? draft.PayloadJson : draft.WorkingPayloadJson),
            ParseOptionalJson(draft.WarningsJson),
            draft.SchemaId,
            draft.Confidence,
            draft.AiJob.Sources.OrderBy(source => source.SortOrder).Select(ToSourceDto).ToList(),
            EncodeRowVersion(draft.RowVersion));

    private static List<AiJobSourceInputDto> NormalizeSources(CreateAiJobDto dto)
    {
        if (dto.Sources is { Count: > 0 })
        {
            return dto.Sources.Select(source => source with
            {
                SourceHash = NormalizeOptional(source.SourceHash) ??
                    (IsManualSource(source.SourceType) && !string.IsNullOrWhiteSpace(dto.SourceText)
                        ? ComputeHash(dto.SourceText)
                        : null)
            }).ToList();
        }

        if (string.IsNullOrWhiteSpace(dto.SourceType)) return [];
        var sourceId = Guid.TryParse(dto.SourceId, out var parsedSourceId) ? parsedSourceId : (Guid?)null;
        return
        [
            new AiJobSourceInputDto(
                dto.SourceType.Trim(),
                sourceId,
                sourceId.HasValue ? null : NormalizeOptional(dto.SourceId),
                NormalizeOptional(dto.SourceVersion),
                NormalizeOptional(dto.SourceHash) ??
                    (IsManualSource(dto.SourceType) && !string.IsNullOrWhiteSpace(dto.SourceText)
                        ? ComputeHash(dto.SourceText)
                        : null))
        ];
    }

    private static string ResolveSchemaId(string jobType, string? requestedSchemaId)
    {
        if (!string.IsNullOrWhiteSpace(requestedSchemaId)) return requestedSchemaId.Trim();

        return jobType.Trim().ToLowerInvariant() switch
        {
            "meetilyimport" or "meetily_import" => "meetily_import.v4",
            "meetingactionextraction" or "meeting_action_extract" => "meeting_action_extract.v4",
            "chatsummary" or "chat_summary" => "chat_summary.v4",
            "taskdraft" or "task_draft" => "task_draft.v4",
            "assigneerecommendation" or "assignee_recommendation" => "assignee_recommendation.v4",
            "taskbreakdown" or "task_breakdown" => "task_breakdown.v4",
            "acceptancechecklist" or "acceptance_checklist" => "acceptance_checklist.v4",
            "progresssummary" or "progress_summary" => "progress_summary.v4",
            "draftchange" => "draft_change.v4",
            _ => string.Empty
        };
    }

    private static bool IsManualSource(string sourceType)
        => sourceType.Trim().ToLowerInvariant() is "manual" or "manualtext" or "text" or "legacy";

    private static string NormalizeProviderHint(string? providerHint)
        => string.IsNullOrWhiteSpace(providerHint) ? "auto" : providerHint.Trim();

    private async Task<PrivacyProcessingDecision?> EvaluateJobPrivacyAsync(
        AiJob job,
        string? providerHint,
        CancellationToken ct)
    {
        if (!job.Sensitive || _complianceService == null)
        {
            return null;
        }

        var source = job.Sources.OrderBy(item => item.SortOrder).FirstOrDefault();
        return await _complianceService.EvaluateProcessingAsync(new PrivacyProcessingRequest
        {
            TenantId = job.TenantId ?? job.ProjectId ?? Guid.Empty,
            ProjectId = job.ProjectId ?? Guid.Empty,
            UserId = job.RequestedById,
            Purpose = PrivacyPurposeForJob(job.JobType),
            DataClassification = PrivacyDataClasses.SensitiveCollaboration,
            ProviderClass = ProviderClassForHint(providerHint),
            ConsentId = job.ConsentId,
            RetentionPolicyId = job.RetentionPolicyId,
            SourceType = source?.SourceType ?? job.SourceType ?? "ai_job",
            SourceEntityId = source?.SourceEntityId
        }, ct);
    }

    private static string PrivacyPurposeForJob(string jobType)
        => jobType.Contains("meeting", StringComparison.OrdinalIgnoreCase)
            ? PrivacyPurposes.MeetingActionExtraction
            : PrivacyPurposes.AiCloudProcessing;

    private static string ProviderClassForHint(string? providerHint)
        => providerHint?.Trim().ToLowerInvariant() switch
        {
            "local" or "ollama" => PrivacyProviderClasses.Local,
            "openai" or "gemini" or "cloud" => PrivacyProviderClasses.Cloud,
            _ => PrivacyProviderClasses.Any
        };

    private static bool IsConsentError(string? errorCode)
        => errorCode is PrivacyErrorCodes.ConsentRequired or
            PrivacyErrorCodes.ConsentInvalid or
            PrivacyErrorCodes.ConsentRevoked or
            PrivacyErrorCodes.ConsentExpired;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IRepository<AiJobDispatch> RequireDispatchRepository()
        => _aiDispatchRepo ?? throw new InvalidOperationException("AI dispatch repository is not registered.");

    private IRepository<AiJobSource> RequireSourceRepository()
        => _aiSourceRepo ?? throw new InvalidOperationException("AI source repository is not registered.");

    private static string ComputeHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static JsonElement ParseJson(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static JsonElement? ParseOptionalJson(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ParseJson(value);

    private static string EncodeRowVersion(byte[] rowVersion)
        => rowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(rowVersion);

    private static bool MatchesRowVersion(byte[] current, string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return current.Length == 0;
        try
        {
            return current.AsSpan().SequenceEqual(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsTaskDraft(string draftType)
        => string.Equals(draftType, "MeetingActionItems", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(draftType, "TaskDraft", StringComparison.OrdinalIgnoreCase);

    private static decimal EstimateCost(string? sourceText)
    {
        var characters = Math.Max(200, sourceText?.Length ?? 200);
        return Math.Round((characters / 4000m) * 0.002m, 6, MidpointRounding.AwayFromZero);
    }

    private static string GenerateCacheKey(string jobType, Guid projectId, string sourceType, string? sourceId, string? sourceText)
    {
        var raw = $"{jobType}|{projectId}|{sourceType}|{sourceId}|{sourceText}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static AiTaskDraftPayload BuildTaskDraftPayload(string? sourceText)
    {
        var lines = (sourceText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(5)
            .ToList();

        if (lines.Count == 0)
        {
            return new AiTaskDraftPayload([
                new AiTaskDraftItem(
                    "Follow up AI action items",
                    "Review meeting context and confirm final task details before creating tasks.")
            ]);
        }

        var tasks = lines
            .Select(line => new AiTaskDraftItem(line, "Generated from AI source text. Please review before confirm."))
            .ToList();
        return new AiTaskDraftPayload(tasks);
    }

    private static AiTaskDraftPayload DeserializeTaskDraftPayload(string payloadJson, string draftType)
    {
        if (string.Equals(draftType, "MeetingActionItems", StringComparison.OrdinalIgnoreCase))
        {
            var meetingPayload = JsonSerializer.Deserialize<MeetingExtractionPayload>(payloadJson, JsonOptions)
                ?? new MeetingExtractionPayload("meetily-import.v1", string.Empty, new MeetingSummaryDto(string.Empty, null, null, []), [], [], []);

            return new AiTaskDraftPayload(meetingPayload.ActionItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .Select(item => new AiTaskDraftItem(
                    item.Title,
                    item.Description ?? item.SourceEvidence,
                    item.Priority,
                    "Todo",
                    item.DueDate,
                    null))
                .ToList());
        }

        return JsonSerializer.Deserialize<AiTaskDraftPayload>(payloadJson, JsonOptions) ?? new AiTaskDraftPayload([]);
    }

    private static MeetingExtractionPayload? TryDeserializeMeetingExtractionPayload(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<MeetingExtractionPayload>(payloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<bool> CanAccessProjectAsync(Project project, Guid currentUserId, CancellationToken ct)
    {
        if (IsAdmin() || project.OwnerId == currentUserId)
        {
            return true;
        }

        var isProjectMember = await _projectMemberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == project.Id && member.UserId == currentUserId, ct);
        if (isProjectMember)
        {
            return true;
        }

        if (!project.OrganizationId.HasValue)
        {
            return false;
        }

        if (project.Organization?.OwnerId == currentUserId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == currentUserId, ct);
    }

    private async Task<bool> CanManageProjectAsync(Project project, Guid currentUserId, CancellationToken ct)
    {
        if (IsAdmin() || project.OwnerId == currentUserId)
        {
            return true;
        }

        var projectRole = await _projectMemberRepo.GetQueryable()
            .Where(member => member.ProjectId == project.Id && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        if (ProjectRoleRules.CanManageProject(projectRole))
        {
            return true;
        }

        if (!project.OrganizationId.HasValue)
        {
            return false;
        }

        if (project.Organization?.OwnerId == currentUserId)
        {
            return true;
        }

        var organizationRole = await _organizationMemberRepo.GetQueryable()
            .Where(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return ProjectRoleRules.CanManageProject(organizationRole);
    }

    private async Task<bool> IsProjectUserAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (project.OwnerId == userId)
        {
            return true;
        }

        if (await _projectMemberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == project.Id && member.UserId == userId, ct))
        {
            return true;
        }

        if (!project.OrganizationId.HasValue)
        {
            return false;
        }

        if (project.Organization?.OwnerId == userId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == userId, ct);
    }

    private bool IsAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);
}
