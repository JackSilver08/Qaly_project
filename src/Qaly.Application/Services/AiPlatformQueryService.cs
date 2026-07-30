using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Globalization;

namespace Qaly.Application.Services;

public sealed class AiPlatformQueryService : IAiPlatformQueryService
{
    private readonly IRepository<AiJob> _jobs;
    private readonly IRepository<AiJobDispatch> _dispatches;
    private readonly IRepository<AiUsageLedger> _usage;
    private readonly IRepository<AiBudgetPolicy> _budgets;
    private readonly IRepository<Organization> _organizations;
    private readonly IRepository<Project> _projects;
    private readonly IRepository<ProjectMember> _projectMembers;
    private readonly IRepository<OrganizationMember> _organizationMembers;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptionsMonitor<AiJobPlatformOptions> _options;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLog;

    public AiPlatformQueryService(
        IRepository<AiJob> jobs,
        IRepository<AiJobDispatch> dispatches,
        IRepository<AiUsageLedger> usage,
        IRepository<AiBudgetPolicy> budgets,
        IRepository<Organization> organizations,
        IRepository<Project> projects,
        IRepository<ProjectMember> projectMembers,
        IRepository<OrganizationMember> organizationMembers,
        ICurrentUserService currentUser,
        IOptionsMonitor<AiJobPlatformOptions> options,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLog)
    {
        _jobs = jobs;
        _dispatches = dispatches;
        _usage = usage;
        _budgets = budgets;
        _organizations = organizations;
        _projects = projects;
        _projectMembers = projectMembers;
        _organizationMembers = organizationMembers;
        _currentUser = currentUser;
        _options = options;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
    }

    public async Task<Result<AiPlatformHealthDto>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue) return Result.Forbidden<AiPlatformHealthDto>();
        var now = DateTimeOffset.UtcNow;
        var visibleJobIds = VisibleJobs().Select(job => job.Id);
        var queue = _dispatches.GetQueryable().Where(dispatch => visibleJobIds.Contains(dispatch.AiJobId));
        var queueDepth = await queue.CountAsync(dispatch => dispatch.CompletedAt == null, cancellationToken);
        var running = await VisibleJobs().CountAsync(job => job.Status == AiJobStatuses.Running, cancellationToken);
        var retrying = await VisibleJobs().CountAsync(job => job.Status == AiJobStatuses.Retrying, cancellationToken);
        var failed = await VisibleJobs().CountAsync(
            job => job.Status == AiJobStatuses.Failed && job.FinishedAt >= now.AddHours(-24),
            cancellationToken);
        var expiredLeases = await queue.CountAsync(
            dispatch => dispatch.CompletedAt == null && dispatch.LeaseExpiresAt != null && dispatch.LeaseExpiresAt <= now,
            cancellationToken);
        var oldest = await queue
            .Where(dispatch => dispatch.CompletedAt == null)
            .OrderBy(dispatch => dispatch.AvailableAt)
            .Select(dispatch => (DateTimeOffset?)dispatch.AvailableAt)
            .FirstOrDefaultAsync(cancellationToken);

        var options = _options.CurrentValue;
        string? degradedReason = null;
        if (!options.Enabled) degradedReason = "platform_disabled";
        else if (!options.WorkerEnabled) degradedReason = "worker_disabled";
        else if (expiredLeases > 0) degradedReason = "expired_leases";

        return Result.Success(new AiPlatformHealthDto(
            degradedReason == null ? "healthy" : "degraded",
            options.Enabled,
            options.WorkerEnabled,
            queueDepth,
            running,
            retrying,
            failed,
            expiredLeases,
            oldest.HasValue ? Math.Max(0, (now - oldest.Value).TotalSeconds) : null,
            degradedReason,
            now));
    }

    public async Task<Result<IReadOnlyList<AiBudgetScopeDto>>> GetBudgetScopesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue) return Result.Forbidden<IReadOnlyList<AiBudgetScopeDto>>();

        var userId = _currentUser.UserId.Value;
        var isSystemAdmin = ProjectRoleRules.IsSystemAdmin(_currentUser.Role);
        var manageableOrganizationIds = isSystemAdmin
            ? _organizations.GetQueryable().Select(item => item.Id)
            : _organizations.GetQueryable()
                .Where(item => item.OwnerId == userId || item.Members.Any(member =>
                    member.UserId == userId &&
                    (member.Role == OrganizationRoleRules.Owner ||
                     member.Role == OrganizationRoleRules.OrganizationAdmin ||
                     member.Role == OrganizationRoleRules.BillingAdmin ||
                     member.Role == "Admin" ||
                     member.Role == "Manager")))
                .Select(item => item.Id);

        var organizations = await _organizations.GetQueryable().AsNoTracking()
            .Where(item => item.IsActive && manageableOrganizationIds.Contains(item.Id))
            .OrderBy(item => item.Name)
            .Select(item => new AiBudgetScopeDto("organization", item.Id, item.Id, null, item.Name))
            .ToListAsync(cancellationToken);

        var manageableProjectIds = isSystemAdmin
            ? _projects.GetQueryable().Select(item => item.Id)
            : _projects.GetQueryable()
                .Where(item =>
                    item.OwnerId == userId ||
                    item.Members.Any(member => member.UserId == userId &&
                        (member.Role == ProjectRoleRules.Owner ||
                         member.Role == ProjectRoleRules.Manager ||
                         member.Role == ProjectRoleRules.ScrumMaster ||
                         member.Role == "PM" ||
                         member.Role == "ProjectOwner" ||
                         member.Role == "ProjectManager" ||
                         member.Role == "Admin")) ||
                    (item.OrganizationId.HasValue && manageableOrganizationIds.Contains(item.OrganizationId.Value)))
                .Select(item => item.Id);

        var projects = await _projects.GetQueryable().AsNoTracking()
            .Where(item => !item.IsDeleted && manageableProjectIds.Contains(item.Id))
            .OrderBy(item => item.Name)
            .Select(item => new AiBudgetScopeDto("project", item.Id, item.OrganizationId, item.Id, item.Name))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AiBudgetScopeDto>>([.. organizations, .. projects]);
    }

    public async Task<Result<AiUsageSnapshotDto>> GetUsageAsync(
        Guid? organizationId,
        Guid? projectId,
        DateTimeOffset? rangeStart,
        DateTimeOffset? rangeEnd,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ResolveScopeAsync(organizationId, projectId, cancellationToken);
        if (!scopeResult.IsSuccess || scopeResult.Data == null)
        {
            return Result.Failure<AiUsageSnapshotDto>(
                scopeResult.Error ?? "AI budget scope was not found.",
                scopeResult.StatusCode,
                scopeResult.ErrorCode);
        }

        var scope = scopeResult.Data;
        var rangeTo = rangeEnd ?? DateTimeOffset.UtcNow;
        var rangeFrom = rangeStart ?? rangeTo.AddDays(-30);
        if (rangeFrom >= rangeTo || rangeTo - rangeFrom > TimeSpan.FromDays(366))
        {
            return Result.Failure<AiUsageSnapshotDto>(
                "from must be before to and the range cannot exceed 366 days.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        var query = _usage.GetQueryable().AsNoTracking()
            .Where(entry => entry.CreatedAt >= rangeFrom && entry.CreatedAt < rangeTo);
        query = scope.ProjectId.HasValue
            ? query.Where(entry => entry.ProjectId == scope.ProjectId.Value)
            : query.Where(entry => entry.TenantId == scope.OrganizationId);

        var rows = await query.ToListAsync(cancellationToken);
        return Result.Success(new AiUsageSnapshotDto(
            scope.ScopeType,
            scope.ScopeId,
            scope.OrganizationId,
            scope.ProjectId,
            scope.Name,
            rangeFrom,
            rangeTo,
            rows.Count,
            rows.Count(IsSuccessful),
            rows.Count(IsFailed),
            rows.Sum(entry => entry.InputTokens),
            rows.Sum(entry => entry.OutputTokens),
            rows.Sum(entry => entry.EstimatedCostUsd),
            rows.Sum(EffectiveCost),
            rows.Count(entry => entry.ActualCostUsd.HasValue),
            rows.Count(entry => !entry.ActualCostUsd.HasValue),
            rows.Count(entry => entry.CacheHit),
            BuildBreakdown(rows, entry => entry.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            BuildBreakdown(rows, entry => EmptyAsUnknown(entry.ProviderName)),
            BuildBreakdown(rows, entry => EmptyAsUnknown(entry.JobType)),
            BuildBreakdown(rows, entry => EmptyAsUnknown(entry.Status)),
            BuildBreakdown(rows, entry => entry.CacheHit ? "hit" : "miss"),
            DateTimeOffset.UtcNow));
    }

    public async Task<Result<AiBudgetSnapshotDto>> GetBudgetAsync(
        Guid? organizationId,
        Guid? projectId,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ResolveScopeAsync(organizationId, projectId, cancellationToken);
        if (!scopeResult.IsSuccess || scopeResult.Data == null)
        {
            return Result.Failure<AiBudgetSnapshotDto>(
                scopeResult.Error ?? "AI budget scope was not found.",
                scopeResult.StatusCode,
                scopeResult.ErrorCode);
        }

        return await BuildBudgetSnapshotAsync(scopeResult.Data, cancellationToken);
    }

    public async Task<Result<AiBudgetSnapshotDto>> UpdateBudgetAsync(
        Guid? organizationId,
        Guid? projectId,
        UpdateAiBudgetPolicyDto dto,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await ResolveScopeAsync(organizationId, projectId, cancellationToken);
        if (!scopeResult.IsSuccess || scopeResult.Data == null)
        {
            return Result.Failure<AiBudgetSnapshotDto>(
                scopeResult.Error ?? "AI budget scope was not found.",
                scopeResult.StatusCode,
                scopeResult.ErrorCode);
        }

        var scope = scopeResult.Data;
        if (!_options.CurrentValue.BudgetUiEnabled)
        {
            return Result.Failure<AiBudgetSnapshotDto>(
                "AI budget policy editing is disabled.",
                503,
                AiErrorCodes.PlatformDisabled);
        }

        if (!dto.Confirmed)
        {
            return Result.Failure<AiBudgetSnapshotDto>(
                "Explicit confirmation is required before changing an AI budget policy.",
                400,
                AiErrorCodes.BudgetConfirmationRequired);
        }

        if (dto.DailyBudgetUsd <= 0 ||
            dto.MonthlyBudgetUsd <= 0 ||
            dto.DailyBudgetUsd > dto.MonthlyBudgetUsd ||
            dto.MonthlyBudgetUsd > 1_000_000m ||
            dto.WarningAtPercent is < 1 or > 100)
        {
            return Result.Failure<AiBudgetSnapshotDto>(
                "Budgets must be positive, daily cannot exceed monthly, monthly cannot exceed 1,000,000 USD, and warningAtPercent must be between 1 and 100.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        var policy = await DirectPolicyQuery(scope).FirstOrDefaultAsync(cancellationToken);
        var previous = policy == null ? null : PolicyAuditSnapshot(policy);
        if (policy != null && !string.Equals(dto.Version, PolicyVersion(policy), StringComparison.Ordinal))
        {
            return BudgetConflict();
        }

        var isNewPolicy = false;
        if (policy == null)
        {
            if (!string.IsNullOrWhiteSpace(dto.Version)) return BudgetConflict();
            isNewPolicy = true;
            policy = new AiBudgetPolicy
            {
                TenantId = scope.OrganizationId,
                ProjectId = scope.ProjectId,
                CreatedBy = _currentUser.UserId
            };
            await _budgets.AddAsync(policy, cancellationToken);
        }

        policy.DailyBudgetUsd = dto.DailyBudgetUsd;
        policy.MonthlyBudgetUsd = dto.MonthlyBudgetUsd;
        policy.WarnAtPercent = dto.WarningAtPercent;
        policy.HardStopEnabled = dto.HardStopEnabled;
        policy.AllowCloudForSensitive = dto.AllowCloudForSensitive;
        if (!isNewPolicy)
        {
            await _budgets.UpdateAsync(policy, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BudgetConflict();
        }
        catch (DbUpdateException)
        {
            return BudgetConflict();
        }

        await _auditLog.LogAsync(
            "UpdateAiBudgetPolicy",
            nameof(AiBudgetPolicy),
            policy.Id.ToString(),
            new
            {
                scope.ScopeType,
                scope.ScopeId,
                scope.OrganizationId,
                scope.ProjectId,
                Previous = previous,
                Current = PolicyAuditSnapshot(policy)
            },
            cancellationToken);

        return await BuildBudgetSnapshotAsync(scope, cancellationToken);
    }

    private IQueryable<AiJob> VisibleJobs()
    {
        var userId = _currentUser.UserId!.Value;
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role)) return _jobs.GetQueryable();
        var memberProjects = _projectMembers.GetQueryable().Where(member => member.UserId == userId).Select(member => member.ProjectId);
        var organizations = _organizationMembers.GetQueryable().Where(member => member.UserId == userId).Select(member => member.OrganizationId);
        return _jobs.GetQueryable().Where(job =>
            job.RequestedById == userId ||
            (job.ProjectId.HasValue && memberProjects.Contains(job.ProjectId.Value)) ||
            (job.TenantId.HasValue && organizations.Contains(job.TenantId.Value)) ||
            (job.Project != null && job.Project.OwnerId == userId));
    }

    private async Task<Result<ResolvedBudgetScope>> ResolveScopeAsync(
        Guid? organizationId,
        Guid? projectId,
        CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<ResolvedBudgetScope>(
                "Authentication is required.",
                403,
                AiErrorCodes.PermissionDenied);
        }

        if (!organizationId.HasValue && !projectId.HasValue)
        {
            return Result.Failure<ResolvedBudgetScope>(
                "organizationId or projectId is required.",
                400,
                AiErrorCodes.InvalidRequest);
        }

        if (projectId.HasValue)
        {
            var project = await _projects.GetQueryable().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == projectId.Value && !item.IsDeleted, ct);
            if (project == null ||
                (organizationId.HasValue && project.OrganizationId != organizationId))
            {
                return Result.Failure<ResolvedBudgetScope>(
                    "AI budget scope was not found.",
                    404,
                    AiErrorCodes.PermissionDenied);
            }

            if (!await CanManageProjectAsync(project, ct))
            {
                return Result.Failure<ResolvedBudgetScope>(
                    "AI budget scope was not found.",
                    404,
                    AiErrorCodes.PermissionDenied);
            }

            return Result.Success(new ResolvedBudgetScope(
                "project",
                project.Id,
                project.OrganizationId,
                project.Id,
                project.Name));
        }

        var organization = await _organizations.GetQueryable().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == organizationId!.Value && item.IsActive, ct);
        if (organization == null || !await CanManageOrganizationBudgetAsync(organization, ct))
        {
            return Result.Failure<ResolvedBudgetScope>(
                "AI budget scope was not found.",
                404,
                AiErrorCodes.PermissionDenied);
        }

        return Result.Success(new ResolvedBudgetScope(
            "organization",
            organization.Id,
            organization.Id,
            null,
            organization.Name));
    }

    private async Task<bool> CanManageProjectAsync(Project project, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role)) return true;

        var projectInfo = await _projects.GetQueryable()
            .AsNoTracking()
            .Where(item => item.Id == project.Id)
            .Select(item => new
            {
                item.OrganizationId,
                OrganizationIsActive = item.Organization != null && item.Organization.IsActive,
                OrganizationOwnerId = item.Organization != null ? (Guid?)item.Organization.OwnerId : null
            })
            .FirstOrDefaultAsync(ct);

        if (projectInfo?.OrganizationId == null || projectInfo.OrganizationOwnerId == null || !projectInfo.OrganizationIsActive)
        {
            return false;
        }

        if (project.OwnerId == userId || projectInfo.OrganizationOwnerId == userId) return true;
        var projectRole = await _projectMembers.GetQueryable()
            .Where(member => member.ProjectId == project.Id && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        if (ProjectRoleRules.CanManageProject(projectRole)) return true;
        if (!project.OrganizationId.HasValue) return false;
        var organizationRole = await _organizationMembers.GetQueryable()
            .Where(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        return OrganizationRoleRules.CanManageAiBudget(organizationRole);
    }

    private async Task<bool> CanManageOrganizationBudgetAsync(Organization organization, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role) || organization.OwnerId == userId) return true;
        var role = await _organizationMembers.GetQueryable()
            .Where(member => member.OrganizationId == organization.Id && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        return OrganizationRoleRules.CanManageAiBudget(role);
    }

    private async Task<Result<AiBudgetSnapshotDto>> BuildBudgetSnapshotAsync(
        ResolvedBudgetScope scope,
        CancellationToken ct)
    {
        var directPolicy = await DirectPolicyQuery(scope).AsNoTracking().FirstOrDefaultAsync(ct);
        AiBudgetPolicy? effectivePolicy = directPolicy;
        if (effectivePolicy == null && scope.ProjectId.HasValue && scope.OrganizationId.HasValue)
        {
            effectivePolicy = await _budgets.GetQueryable().AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ProjectId == null && item.TenantId == scope.OrganizationId.Value,
                    ct);
        }

        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var costsQuery = _usage.GetQueryable().AsNoTracking().Where(entry => entry.CreatedAt >= startOfMonth);
        if (effectivePolicy?.ProjectId.HasValue == true ||
            (effectivePolicy == null && scope.ProjectId.HasValue))
        {
            costsQuery = costsQuery.Where(entry => entry.ProjectId == scope.ProjectId);
        }
        else
        {
            costsQuery = costsQuery.Where(entry => entry.TenantId == scope.OrganizationId);
        }

        var costs = await costsQuery
            .Select(entry => new { entry.CreatedAt, Cost = entry.ActualCostUsd ?? entry.EstimatedCostUsd })
            .ToListAsync(ct);
        var dailyUsage = costs.Where(entry => entry.CreatedAt >= startOfDay).Sum(entry => entry.Cost);
        var monthlyUsage = costs.Sum(entry => entry.Cost);
        var dailyLimit = effectivePolicy?.DailyBudgetUsd ?? decimal.MaxValue;
        var monthlyLimit = effectivePolicy?.MonthlyBudgetUsd ?? decimal.MaxValue;
        var warningPercent = effectivePolicy?.WarnAtPercent ?? 80;
        var warningActive = IsThresholdReached(dailyUsage, dailyLimit, warningPercent) ||
                            IsThresholdReached(monthlyUsage, monthlyLimit, warningPercent);
        var hardStop = effectivePolicy?.HardStopEnabled == true &&
                       (dailyUsage >= dailyLimit || monthlyUsage >= monthlyLimit);
        var policySource = effectivePolicy == null
            ? "none"
            : effectivePolicy.ProjectId.HasValue ? "project" : "organization";

        return Result.Success(new AiBudgetSnapshotDto(
            scope.ScopeType,
            scope.ScopeId,
            scope.OrganizationId,
            scope.ProjectId,
            scope.Name,
            directPolicy?.Id,
            effectivePolicy?.Id,
            policySource,
            directPolicy == null && effectivePolicy != null,
            effectivePolicy != null,
            true,
            _options.CurrentValue.BudgetUiEnabled,
            effectivePolicy?.DailyBudgetUsd ?? 0,
            effectivePolicy?.MonthlyBudgetUsd ?? 0,
            warningPercent,
            effectivePolicy?.HardStopEnabled ?? false,
            dailyUsage,
            monthlyUsage,
            dailyLimit == decimal.MaxValue ? 0 : Math.Max(0, dailyLimit - dailyUsage),
            monthlyLimit == decimal.MaxValue ? 0 : Math.Max(0, monthlyLimit - monthlyUsage),
            warningActive,
            hardStop,
            effectivePolicy?.AllowCloudForSensitive ?? false,
            PolicyVersion(directPolicy),
            PolicyVersion(effectivePolicy),
            now));
    }

    private IQueryable<AiBudgetPolicy> DirectPolicyQuery(ResolvedBudgetScope scope)
        => scope.ProjectId.HasValue
            ? _budgets.GetQueryable().Where(item => item.ProjectId == scope.ProjectId.Value)
            : _budgets.GetQueryable().Where(item =>
                item.ProjectId == null && item.TenantId == scope.OrganizationId);

    private static List<AiUsageBreakdownDto> BuildBreakdown(
        IReadOnlyList<AiUsageLedger> rows,
        Func<AiUsageLedger, string> keySelector)
        => rows
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AiUsageBreakdownDto(
                group.Key,
                group.Count(),
                group.Count(IsSuccessful),
                group.Count(IsFailed),
                group.Sum(entry => entry.InputTokens),
                group.Sum(entry => entry.OutputTokens),
                group.Sum(entry => entry.EstimatedCostUsd),
                group.Sum(EffectiveCost),
                group.Count(entry => entry.CacheHit)))
            .OrderByDescending(item => item.EffectiveCostUsd)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsSuccessful(AiUsageLedger entry)
        => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(entry.Status, "succeeded", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailed(AiUsageLedger entry)
        => string.Equals(entry.Status, "failed", StringComparison.OrdinalIgnoreCase);

    private static decimal EffectiveCost(AiUsageLedger entry)
        => entry.ActualCostUsd ?? entry.EstimatedCostUsd;

    private static string EmptyAsUnknown(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static object PolicyAuditSnapshot(AiBudgetPolicy policy)
        => new
        {
            policy.DailyBudgetUsd,
            policy.MonthlyBudgetUsd,
            policy.WarnAtPercent,
            policy.HardStopEnabled,
            policy.AllowCloudForSensitive,
            Version = PolicyVersion(policy)
        };

    private static Result<AiBudgetSnapshotDto> BudgetConflict()
        => Result.Failure<AiBudgetSnapshotDto>(
            "The AI budget policy was modified by another request. Reload before confirming again.",
            409,
            AiErrorCodes.BudgetPolicyConflict);

    private sealed record ResolvedBudgetScope(
        string ScopeType,
        Guid ScopeId,
        Guid? OrganizationId,
        Guid? ProjectId,
        string Name);

    private static string? PolicyVersion(AiBudgetPolicy? policy)
        => policy == null ? null : (policy.UpdatedAt ?? policy.CreatedAt).ToString("O");

    private static bool IsThresholdReached(decimal usage, decimal limit, int warningPercent)
        => limit != decimal.MaxValue && limit > 0 && usage / limit * 100 >= warningPercent;
}
