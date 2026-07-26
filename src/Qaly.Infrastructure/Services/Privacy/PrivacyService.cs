using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Privacy;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.Privacy;

public sealed partial class PrivacyService : IPrivacyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly QalyDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiComplianceService _compliance;
    private readonly IPrivacyPayloadProtector _payloadProtector;
    private readonly PrivacyV4Options _options;

    public PrivacyService(
        QalyDbContext db,
        ICurrentUserService currentUser,
        IAiComplianceService compliance,
        IPrivacyPayloadProtector payloadProtector,
        IOptions<PrivacyV4Options> options)
    {
        _db = db;
        _currentUser = currentUser;
        _compliance = compliance;
        _payloadProtector = payloadProtector;
        _options = options.Value;
    }

    public async Task<Result<IReadOnlyList<RetentionPolicyDto>>> ListPoliciesAsync(
        Guid tenantId,
        Guid? projectId,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue || !await CanAccessTenantAsync(tenantId, projectId, userId.Value, ct))
        {
            return Result.Forbidden<IReadOnlyList<RetentionPolicyDto>>();
        }

        var policies = await _db.RetentionPolicies
            .AsNoTracking()
            .Where(policy => policy.TenantId == tenantId &&
                (!projectId.HasValue || policy.ProjectId == projectId || policy.ProjectId == null))
            .OrderByDescending(policy => policy.IsActive)
            .ThenByDescending(policy => policy.EffectiveFrom)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<RetentionPolicyDto>>(policies.Select(ToPolicyDto).ToList());
    }

    public async Task<Result<RetentionPolicyDto>> CreatePolicyAsync(
        RetentionPolicyUpsertRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue || !await CanManageTenantAsync(request.TenantId, request.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<RetentionPolicyDto>();
        }

        var validationError = ValidatePolicyRequest(request);
        if (validationError != null)
        {
            return Result.Failure<RetentionPolicyDto>(validationError, 400, PrivacyErrorCodes.PolicyMismatch);
        }

        if (request.ProjectId.HasValue &&
            !await ProjectBelongsToTenantAsync(request.ProjectId.Value, request.TenantId, ct))
        {
            return Result.Failure<RetentionPolicyDto>(
                "The project does not belong to the requested tenant.",
                400,
                PrivacyErrorCodes.PolicyMismatch);
        }

        var policy = BuildPolicy(request, userId.Value);
        _db.RetentionPolicies.Add(policy);
        AddAudit(
            tenantId: policy.TenantId,
            projectId: policy.ProjectId,
            actorUserId: userId,
            eventType: "RETENTION_POLICY_CREATED",
            entityType: nameof(RetentionPolicy),
            entityId: policy.Id,
            policyId: policy.Id,
            purpose: policy.Purpose,
            policyVersion: policy.PolicyVersion,
            dataClassification: policy.DataClassification,
            providerClass: policy.AllowCloudProcessing ? PrivacyProviderClasses.Any : PrivacyProviderClasses.Local,
            outcome: "created",
            metadata: new Dictionary<string, string?>
            {
                ["expiryAction"] = policy.ExpiryAction,
                ["defaultRetentionDays"] = policy.DefaultRetentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        await _db.SaveChangesAsync(ct);
        return Result.Created(ToPolicyDto(policy));
    }

    public async Task<Result<RetentionPolicyDto>> SupersedePolicyAsync(
        Guid policyId,
        RetentionPolicyUpsertRequest request,
        string rowVersion,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var existing = await _db.RetentionPolicies.FirstOrDefaultAsync(policy => policy.Id == policyId, ct);
        if (existing == null)
        {
            return Result.NotFound<RetentionPolicyDto>("Retention policy was not found.");
        }

        if (!userId.HasValue || !await CanManageTenantAsync(existing.TenantId, existing.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<RetentionPolicyDto>();
        }

        if (!MatchesRowVersion(existing.RowVersion, rowVersion))
        {
            return Result.Failure<RetentionPolicyDto>(
                "The retention policy was changed by another request.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        if (request.TenantId != existing.TenantId || request.ProjectId != existing.ProjectId)
        {
            return Result.Failure<RetentionPolicyDto>(
                "A policy version cannot move between tenants or projects.",
                400,
                PrivacyErrorCodes.PolicyMismatch);
        }

        var validationError = ValidatePolicyRequest(request);
        if (validationError != null)
        {
            return Result.Failure<RetentionPolicyDto>(validationError, 400, PrivacyErrorCodes.PolicyMismatch);
        }

        var now = DateTimeOffset.UtcNow;
        existing.IsActive = false;
        existing.EffectiveUntil ??= now;
        existing.UpdatedById = userId;
        var replacement = BuildPolicy(request, userId.Value, now);
        _db.RetentionPolicies.Add(replacement);
        AddAudit(
            existing.TenantId,
            existing.ProjectId,
            userId,
            "RETENTION_POLICY_SUPERSEDED",
            nameof(RetentionPolicy),
            replacement.Id,
            policyId: replacement.Id,
            purpose: replacement.Purpose,
            policyVersion: replacement.PolicyVersion,
            dataClassification: replacement.DataClassification,
            providerClass: replacement.AllowCloudProcessing ? PrivacyProviderClasses.Any : PrivacyProviderClasses.Local,
            outcome: "superseded",
            metadata: new Dictionary<string, string?>
            {
                ["previousPolicyId"] = existing.Id.ToString(),
                ["previousPolicyVersion"] = existing.PolicyVersion
            });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<RetentionPolicyDto>(
                "The retention policy was changed by another request.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        return Result.Created(ToPolicyDto(replacement));
    }

    public async Task<Result<RetentionPolicyDto>> DisablePolicyAsync(
        Guid policyId,
        RetentionPolicyDisableRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var policy = await _db.RetentionPolicies.FirstOrDefaultAsync(item => item.Id == policyId, ct);
        if (policy == null)
        {
            return Result.NotFound<RetentionPolicyDto>("Retention policy was not found.");
        }

        if (!userId.HasValue || !await CanManageTenantAsync(policy.TenantId, policy.ProjectId, userId.Value, ct))
        {
            return Result.Forbidden<RetentionPolicyDto>();
        }

        if (!MatchesRowVersion(policy.RowVersion, request.RowVersion))
        {
            return Result.Failure<RetentionPolicyDto>(
                "The retention policy was changed by another request.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        policy.IsActive = false;
        policy.EffectiveUntil ??= DateTimeOffset.UtcNow;
        policy.UpdatedById = userId;
        AddAudit(
            policy.TenantId,
            policy.ProjectId,
            userId,
            "RETENTION_POLICY_DISABLED",
            nameof(RetentionPolicy),
            policy.Id,
            policyId: policy.Id,
            purpose: policy.Purpose,
            policyVersion: policy.PolicyVersion,
            dataClassification: policy.DataClassification,
            outcome: "disabled",
            metadata: new Dictionary<string, string?>
            {
                ["reasonHash"] = HashText(request.Reason ?? string.Empty)
            });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<RetentionPolicyDto>(
                "The retention policy was changed by another request.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        return Result.Success(ToPolicyDto(policy));
    }

    public async Task<Result<IReadOnlyList<PrivacyConsentDto>>> ListConsentsAsync(
        Guid? projectId,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
        {
            return Result.Forbidden<IReadOnlyList<PrivacyConsentDto>>();
        }

        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value, userId.Value, ct))
        {
            return Result.Forbidden<IReadOnlyList<PrivacyConsentDto>>();
        }

        var query = _db.PrivacyConsents.AsNoTracking().Where(consent => consent.UserId == userId.Value);
        if (projectId.HasValue)
        {
            query = query.Where(consent => consent.ProjectId == projectId.Value);
        }

        var consents = await query.OrderByDescending(consent => consent.GrantedAt).ToListAsync(ct);
        return Result.Success<IReadOnlyList<PrivacyConsentDto>>(consents.Select(ToConsentDto).ToList());
    }

    public async Task<Result<PrivacyConsentDto>> GrantConsentAsync(
        PrivacyConsentGrantRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var project = userId.HasValue
            ? await GetAccessibleProjectAsync(request.ProjectId, userId.Value, ct)
            : null;
        if (!userId.HasValue || project == null)
        {
            return Result.Forbidden<PrivacyConsentDto>();
        }

        var policy = await _db.RetentionPolicies
            .FirstOrDefaultAsync(item => item.Id == request.RetentionPolicyId, ct);
        var tenantId = project.OrganizationId ?? project.Id;
        var providerClass = NormalizeProviderClass(request.ProviderClass);
        if (policy == null || !policy.IsActive || policy.TenantId != tenantId ||
            (policy.ProjectId.HasValue && policy.ProjectId != project.Id) ||
            !string.Equals(policy.Purpose, request.Purpose, StringComparison.Ordinal) ||
            providerClass == PrivacyProviderClasses.Unknown ||
            string.IsNullOrWhiteSpace(request.NoticeVersion) || request.NoticeVersion.Length > 80 ||
            string.IsNullOrWhiteSpace(request.SourceType) || request.SourceType.Length > 50 ||
            (request.ExpiresAt.HasValue && request.ExpiresAt <= DateTimeOffset.UtcNow))
        {
            return Result.Failure<PrivacyConsentDto>(
                "The consent scope, provider class, policy, notice, or expiry is invalid.",
                400,
                PrivacyErrorCodes.ConsentInvalid);
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await _db.PrivacyConsents
            .Where(consent => consent.TenantId == tenantId &&
                consent.ProjectId == project.Id &&
                consent.UserId == userId.Value &&
                consent.Purpose == request.Purpose &&
                consent.SourceType == request.SourceType &&
                consent.SourceEntityId == request.SourceEntityId &&
                consent.Status == PrivacyConsentStatuses.Granted)
            .ToListAsync(ct);
        foreach (var consent in existing)
        {
            consent.Status = PrivacyConsentStatuses.Revoked;
            consent.RevokedAt = now;
            consent.RevokedById = userId;
        }

        var granted = new PrivacyConsent
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            ConsentType = providerClass is PrivacyProviderClasses.Cloud or PrivacyProviderClasses.Any
                ? PrivacyPurposes.AiCloudProcessing
                : request.Purpose,
            Purpose = request.Purpose.Trim(),
            SourceType = request.SourceType.Trim(),
            SourceEntityId = request.SourceEntityId,
            ProviderClass = providerClass,
            PolicyVersion = policy.PolicyVersion,
            NoticeVersion = request.NoticeVersion.Trim(),
            RetentionPolicyId = policy.Id,
            ScopeJson = JsonSerializer.Serialize(new
            {
                projectId = project.Id,
                sourceType = request.SourceType.Trim(),
                sourceEntityId = request.SourceEntityId
            }, JsonOptions),
            EvidenceJson = JsonSerializer.Serialize(new
            {
                method = "interactive",
                noticeVersion = request.NoticeVersion.Trim()
            }, JsonOptions),
            Status = PrivacyConsentStatuses.Granted,
            GrantedAt = now,
            GrantedById = userId,
            ExpiresAt = request.ExpiresAt
        };
        _db.PrivacyConsents.Add(granted);
        AddAudit(
            tenantId,
            project.Id,
            userId,
            "PRIVACY_CONSENT_GRANTED",
            nameof(PrivacyConsent),
            granted.Id,
            consentId: granted.Id,
            policyId: policy.Id,
            purpose: granted.Purpose,
            policyVersion: policy.PolicyVersion,
            dataClassification: policy.DataClassification,
            providerClass: granted.ProviderClass,
            outcome: "granted",
            metadata: new Dictionary<string, string?>
            {
                ["sourceType"] = granted.SourceType,
                ["sourceEntityId"] = granted.SourceEntityId?.ToString()
            });
        await _db.SaveChangesAsync(ct);
        return Result.Created(ToConsentDto(granted));
    }

    public async Task<Result<PrivacyConsentDto>> RevokeConsentAsync(
        Guid consentId,
        PrivacyConsentRevokeRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var consent = await _db.PrivacyConsents
            .Include(item => item.RetentionPolicy)
            .FirstOrDefaultAsync(item => item.Id == consentId, ct);
        if (consent == null)
        {
            return Result.NotFound<PrivacyConsentDto>("Consent was not found.");
        }

        if (!userId.HasValue || consent.UserId != userId.Value)
        {
            return Result.Forbidden<PrivacyConsentDto>();
        }

        if (!MatchesRowVersion(consent.RowVersion, request.RowVersion))
        {
            return Result.Failure<PrivacyConsentDto>(
                "Consent was changed by another request.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        if (consent.Status != PrivacyConsentStatuses.Granted)
        {
            return Result.Success(ToConsentDto(consent));
        }

        var now = DateTimeOffset.UtcNow;
        consent.Status = PrivacyConsentStatuses.Revoked;
        consent.RevokedAt = now;
        consent.RevokedById = userId;

        var meetings = await _db.MeetingImports
            .Where(import => import.ConsentId == consent.Id && import.PrivacyState == MeetingPrivacyStates.Active)
            .ToListAsync(ct);
        var existingActionIds = await _db.PrivacyRetentionActions
            .Where(action => action.EntityType == nameof(MeetingImport) && meetings.Select(item => item.Id).Contains(action.EntityId))
            .Select(action => action.EntityId)
            .ToHashSetAsync(ct);
        if (consent.RetentionPolicy != null)
        {
            foreach (var meeting in meetings.Where(item => !existingActionIds.Contains(item.Id)))
            {
                _db.PrivacyRetentionActions.Add(new PrivacyRetentionAction
                {
                    TenantId = meeting.TenantId ?? consent.TenantId ?? meeting.ProjectId,
                    ProjectId = meeting.ProjectId,
                    RetentionPolicyId = consent.RetentionPolicy.Id,
                    EntityType = nameof(MeetingImport),
                    EntityId = meeting.Id,
                    ActionType = consent.RetentionPolicy.ExpiryAction,
                    Status = PrivacyWorkerStatuses.Pending,
                    DueAt = now,
                    AvailableAt = now,
                    MaxAttempts = Math.Max(1, _options.MaxAttempts)
                });
            }
        }

        AddAudit(
            consent.TenantId,
            consent.ProjectId,
            userId,
            "PRIVACY_CONSENT_REVOKED",
            nameof(PrivacyConsent),
            consent.Id,
            consentId: consent.Id,
            policyId: consent.RetentionPolicyId,
            purpose: consent.Purpose,
            policyVersion: consent.PolicyVersion,
            providerClass: consent.ProviderClass,
            outcome: "revoked",
            metadata: new Dictionary<string, string?>
            {
                ["reasonHash"] = HashText(request.Reason ?? string.Empty),
                ["scheduledActionCount"] = meetings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PrivacyConsentDto>(
                "Consent was changed by another request.",
                409,
                PrivacyErrorCodes.InvalidTransition);
        }

        return Result.Success(ToConsentDto(consent));
    }

    public async Task<Result<PrivacyDecisionDto>> EvaluateAsync(
        PrivacyDecisionRequest request,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId();
        var project = userId.HasValue
            ? await GetAccessibleProjectAsync(request.ProjectId, userId.Value, ct)
            : null;
        if (!userId.HasValue || project == null)
        {
            return Result.Forbidden<PrivacyDecisionDto>();
        }

        var decision = await _compliance.EvaluateProcessingAsync(new PrivacyProcessingRequest
        {
            TenantId = project.OrganizationId ?? project.Id,
            ProjectId = project.Id,
            UserId = userId.Value,
            Purpose = request.Purpose,
            DataClassification = request.DataClassification,
            ProviderClass = request.ProviderClass,
            ConsentId = request.ConsentId,
            RetentionPolicyId = request.RetentionPolicyId,
            RetentionDays = request.RetentionDays,
            SourceType = request.SourceType,
            SourceEntityId = request.SourceEntityId
        }, ct);
        return Result.Success(ToDecisionDto(decision));
    }

    public async Task<Result<PrivacyHealthDto>> GetHealthAsync(CancellationToken ct = default)
    {
        if (!ProjectRoleRules.IsSystemAdmin(_currentUser.Role) || !CurrentUserId().HasValue)
        {
            return Result.Forbidden<PrivacyHealthDto>();
        }

        var pendingRetention = await _db.PrivacyRetentionActions.LongCountAsync(
            action => action.Status == PrivacyWorkerStatuses.Pending || action.Status == PrivacyWorkerStatuses.Running,
            ct);
        var failedRetention = await _db.PrivacyRetentionActions.LongCountAsync(
            action => action.Status == PrivacyWorkerStatuses.Failed,
            ct);
        var pendingDsar = await _db.DataSubjectRequests.LongCountAsync(
            request => request.Status == DataSubjectRequestStatuses.Accepted || request.Status == DataSubjectRequestStatuses.Collecting,
            ct);
        var failedDsar = await _db.DataSubjectRequests.LongCountAsync(
            request => request.Status == DataSubjectRequestStatuses.Failed,
            ct);
        var oldestRetention = await _db.PrivacyRetentionActions
            .Where(action => action.Status == PrivacyWorkerStatuses.Pending)
            .MinAsync(action => (DateTimeOffset?)action.AvailableAt, ct);
        var oldestDsar = await _db.DataSubjectRequests
            .Where(request => request.Status == DataSubjectRequestStatuses.Accepted)
            .MinAsync(request => (DateTimeOffset?)request.AvailableAt, ct);
        var oldest = new[] { oldestRetention, oldestDsar }.Where(value => value.HasValue).Min();
        var status = !_options.Enabled
            ? "disabled"
            : !_options.WorkerEnabled && (pendingRetention > 0 || pendingDsar > 0)
                ? "degraded_worker_disabled"
                : failedRetention > 0 || failedDsar > 0
                    ? "degraded_failures"
                    : "healthy";

        return Result.Success(new PrivacyHealthDto(
            _options.Enabled,
            _options.EnforceSensitiveIngestion,
            _options.WorkerEnabled,
            status,
            pendingRetention,
            failedRetention,
            pendingDsar,
            failedDsar,
            oldest));
    }

    private static RetentionPolicy BuildPolicy(
        RetentionPolicyUpsertRequest request,
        Guid actorUserId,
        DateTimeOffset? nowOverride = null)
    {
        var now = nowOverride ?? DateTimeOffset.UtcNow;
        var normalizedDays = request.AllowedRetentionDays.Distinct().Order().ToArray();
        var versionMaterial = JsonSerializer.Serialize(new
        {
            request.TenantId,
            request.ProjectId,
            request.Name,
            request.DataClassification,
            request.Purpose,
            normalizedDays,
            request.DefaultRetentionDays,
            request.ExpiryAction,
            request.AllowCloudProcessing,
            request.AllowLocalProcessing,
            request.RequireExplicitConsent,
            effectiveFrom = request.EffectiveFrom ?? now,
            nonce = Guid.NewGuid()
        }, JsonOptions);
        return new RetentionPolicy
        {
            TenantId = request.TenantId,
            ProjectId = request.ProjectId,
            Name = request.Name.Trim(),
            DataClassification = request.DataClassification.Trim().ToLowerInvariant(),
            Purpose = request.Purpose.Trim().ToLowerInvariant(),
            AllowedRetentionDaysJson = JsonSerializer.Serialize(normalizedDays),
            DefaultRetentionDays = request.DefaultRetentionDays,
            ExpiryAction = request.ExpiryAction.Trim().ToLowerInvariant(),
            AllowCloudProcessing = request.AllowCloudProcessing,
            AllowLocalProcessing = request.AllowLocalProcessing,
            RequireExplicitConsent = request.RequireExplicitConsent,
            IsActive = true,
            PolicyVersion = $"p003-{now:yyyyMMddHHmmss}-{HashText(versionMaterial)[..12]}",
            CreatedById = actorUserId,
            ApprovalOwnerUserId = request.ApprovalOwnerUserId,
            EffectiveFrom = request.EffectiveFrom ?? now,
            EffectiveUntil = request.EffectiveUntil
        };
    }

    private string? ValidatePolicyRequest(RetentionPolicyUpsertRequest request)
    {
        var allowedDays = request.AllowedRetentionDays.Distinct().Order().ToArray();
        if (request.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120 ||
            !PrivacyDataClasses.IsSensitive(request.DataClassification) ||
            string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Trim().Length > 80 ||
            allowedDays.Length == 0 ||
            allowedDays.Any(day => !_options.AllowedRetentionDays.Contains(day)) ||
            !allowedDays.Contains(request.DefaultRetentionDays) ||
            request.ExpiryAction is not (PrivacyExpiryActions.Redact or PrivacyExpiryActions.Delete or PrivacyExpiryActions.Review) ||
            (!request.AllowCloudProcessing && !request.AllowLocalProcessing) ||
            (request.EffectiveUntil.HasValue && request.EffectiveUntil <= (request.EffectiveFrom ?? DateTimeOffset.UtcNow)))
        {
            return "The policy name, classification, purpose, retention values, provider settings, or effective period is invalid.";
        }

        return null;
    }

    private async Task<Project?> GetAccessibleProjectAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(item => item.Organization)
            .FirstOrDefaultAsync(item => item.Id == projectId, ct);
        return project != null && await CanAccessProjectAsync(project, userId, ct) ? project : null;
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(item => item.Organization)
            .FirstOrDefaultAsync(item => item.Id == projectId, ct);
        return project != null && await CanAccessProjectAsync(project, userId, ct);
    }

    private async Task<bool> CanAccessProjectAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role) || project.OwnerId == userId || project.Organization?.OwnerId == userId)
        {
            return true;
        }

        if (await _db.ProjectMembers.AnyAsync(member => member.ProjectId == project.Id && member.UserId == userId, ct))
        {
            return true;
        }

        return project.OrganizationId.HasValue && await _db.OrganizationMembers.AnyAsync(
            member => member.OrganizationId == project.OrganizationId.Value && member.UserId == userId,
            ct);
    }

    private async Task<bool> CanAccessTenantAsync(Guid tenantId, Guid? projectId, Guid userId, CancellationToken ct)
    {
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role))
        {
            return true;
        }

        if (projectId.HasValue)
        {
            var project = await _db.Projects
                .Include(item => item.Organization)
                .FirstOrDefaultAsync(item => item.Id == projectId.Value, ct);
            return project != null &&
                (project.OrganizationId ?? project.Id) == tenantId &&
                await CanAccessProjectAsync(project, userId, ct);
        }

        return await _db.Organizations.AnyAsync(organization => organization.Id == tenantId && organization.OwnerId == userId, ct) ||
            await _db.OrganizationMembers.AnyAsync(member => member.OrganizationId == tenantId && member.UserId == userId, ct) ||
            await _db.Projects.AnyAsync(project => (project.OrganizationId ?? project.Id) == tenantId && project.OwnerId == userId, ct) ||
            await _db.ProjectMembers.AnyAsync(member => member.UserId == userId &&
                (member.Project.OrganizationId ?? member.ProjectId) == tenantId, ct);
    }

    private async Task<bool> CanManageTenantAsync(Guid tenantId, Guid? projectId, Guid userId, CancellationToken ct)
    {
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role))
        {
            return true;
        }

        if (projectId.HasValue)
        {
            var project = await _db.Projects
                .Include(item => item.Organization)
                .FirstOrDefaultAsync(item => item.Id == projectId.Value, ct);
            if (project == null || (project.OrganizationId ?? project.Id) != tenantId)
            {
                return false;
            }

            if (project.OwnerId == userId || project.Organization?.OwnerId == userId)
            {
                return true;
            }

            var projectRole = await _db.ProjectMembers
                .Where(member => member.ProjectId == project.Id && member.UserId == userId)
                .Select(member => member.Role)
                .FirstOrDefaultAsync(ct);
            return ProjectRoleRules.CanManageProject(projectRole);
        }

        if (await _db.Organizations.AnyAsync(organization => organization.Id == tenantId && organization.OwnerId == userId, ct))
        {
            return true;
        }

        var organizationRole = await _db.OrganizationMembers
            .Where(member => member.OrganizationId == tenantId && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        return OrganizationRoleRules.CanManageOrganization(organizationRole) ||
            await _db.Projects.AnyAsync(project => project.Id == tenantId && project.OwnerId == userId, ct);
    }

    private Task<bool> ProjectBelongsToTenantAsync(Guid projectId, Guid tenantId, CancellationToken ct)
        => _db.Projects.AnyAsync(project => project.Id == projectId && (project.OrganizationId ?? project.Id) == tenantId, ct);

    private void AddAudit(
        Guid? tenantId,
        Guid? projectId,
        Guid? actorUserId,
        string eventType,
        string entityType,
        Guid? entityId,
        Guid? consentId = null,
        Guid? policyId = null,
        Guid? dsarId = null,
        string? purpose = null,
        string? policyVersion = null,
        string? dataClassification = null,
        string? providerClass = null,
        string outcome = "success",
        string? failureCode = null,
        Dictionary<string, string?>? metadata = null)
    {
        _db.AiAuditEvents.Add(new AiAuditEvent
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ActorUserId = actorUserId,
            EventType = Limit(eventType, 120),
            EntityType = Limit(entityType, 120),
            EntityGuid = entityId,
            EntityKey = entityId?.ToString(),
            PrivacyConsentId = consentId,
            RetentionPolicyId = policyId,
            DataSubjectRequestId = dsarId,
            Purpose = LimitNullable(purpose, 80),
            PolicyVersion = LimitNullable(policyVersion, 80),
            DataClassification = LimitNullable(dataClassification, 50),
            ProviderClass = LimitNullable(providerClass, 30),
            Outcome = Limit(outcome, 40),
            FailureCode = LimitNullable(failureCode, 100),
            AfterJson = metadata == null || metadata.Count == 0
                ? null
                : JsonSerializer.Serialize(metadata.Take(20).ToDictionary(
                    pair => Limit(pair.Key, 80),
                    pair => LimitNullable(pair.Value, 200)), JsonOptions)
        });
    }

    private Guid? CurrentUserId() => _currentUser.UserId;

    private static bool MatchesRowVersion(byte[] current, string supplied)
    {
        try
        {
            return current.AsSpan().SequenceEqual(Convert.FromBase64String(supplied));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncodeRowVersion(byte[] value)
        => value.Length == 0 ? string.Empty : Convert.ToBase64String(value);

    private static string NormalizeProviderClass(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            PrivacyProviderClasses.Local => PrivacyProviderClasses.Local,
            PrivacyProviderClasses.Cloud => PrivacyProviderClasses.Cloud,
            PrivacyProviderClasses.Any => PrivacyProviderClasses.Any,
            _ => PrivacyProviderClasses.Unknown
        };

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Limit(string value, int maximumLength)
    {
        var normalized = value.Trim();
        return normalized[..Math.Min(normalized.Length, maximumLength)];
    }

    private static string? LimitNullable(string? value, int maximumLength)
        => string.IsNullOrWhiteSpace(value) ? null : Limit(value, maximumLength);

    private static RetentionPolicyDto ToPolicyDto(RetentionPolicy policy)
        => new(
            policy.Id,
            policy.TenantId,
            policy.ProjectId,
            policy.Name,
            policy.DataClassification,
            policy.Purpose,
            ParseRetentionDays(policy.AllowedRetentionDaysJson),
            policy.DefaultRetentionDays,
            policy.ExpiryAction,
            policy.AllowCloudProcessing,
            policy.AllowLocalProcessing,
            policy.RequireExplicitConsent,
            policy.IsActive,
            policy.PolicyVersion,
            policy.ApprovalOwnerUserId,
            policy.EffectiveFrom,
            policy.EffectiveUntil,
            EncodeRowVersion(policy.RowVersion));

    private static PrivacyConsentDto ToConsentDto(PrivacyConsent consent)
        => new(
            consent.Id,
            consent.TenantId,
            consent.ProjectId,
            consent.UserId,
            consent.ConsentType,
            consent.Purpose,
            consent.SourceType,
            consent.SourceEntityId,
            consent.ProviderClass,
            consent.PolicyVersion,
            consent.NoticeVersion,
            consent.RetentionPolicyId,
            consent.Status,
            consent.GrantedAt,
            consent.RevokedAt,
            consent.ExpiresAt,
            EncodeRowVersion(consent.RowVersion));

    private static PrivacyDecisionDto ToDecisionDto(PrivacyProcessingDecision decision)
        => new(
            decision.Allowed,
            decision.CloudEligible,
            decision.LocalEligible,
            decision.ErrorCode,
            decision.Reason,
            decision.RetentionPolicyId,
            decision.PolicyVersion,
            decision.ConsentId,
            decision.RetentionDays,
            decision.RetentionExpiresAt,
            decision.ExpiryAction,
            decision.ProviderClass,
            decision.EvaluatedAt);

    private static int[] ParseRetentionDays(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(value)?.Order().ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
