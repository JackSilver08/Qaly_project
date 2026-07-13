using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public sealed class AiPlatformQueryService : IAiPlatformQueryService
{
    private readonly IRepository<AiJob> _jobs;
    private readonly IRepository<AiJobDispatch> _dispatches;
    private readonly IRepository<AiUsageLedger> _usage;
    private readonly IRepository<AiBudgetPolicy> _budgets;
    private readonly IRepository<Project> _projects;
    private readonly IRepository<ProjectMember> _projectMembers;
    private readonly IRepository<OrganizationMember> _organizationMembers;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptionsMonitor<AiJobPlatformOptions> _options;

    public AiPlatformQueryService(
        IRepository<AiJob> jobs,
        IRepository<AiJobDispatch> dispatches,
        IRepository<AiUsageLedger> usage,
        IRepository<AiBudgetPolicy> budgets,
        IRepository<Project> projects,
        IRepository<ProjectMember> projectMembers,
        IRepository<OrganizationMember> organizationMembers,
        ICurrentUserService currentUser,
        IOptionsMonitor<AiJobPlatformOptions> options)
    {
        _jobs = jobs;
        _dispatches = dispatches;
        _usage = usage;
        _budgets = budgets;
        _projects = projects;
        _projectMembers = projectMembers;
        _organizationMembers = organizationMembers;
        _currentUser = currentUser;
        _options = options;
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

    public async Task<Result<AiUsageSnapshotDto>> GetUsageAsync(
        Guid? projectId,
        DateTimeOffset? rangeStart,
        DateTimeOffset? rangeEnd,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue) return Result.Forbidden<AiUsageSnapshotDto>();
        if (projectId.HasValue && !await CanAccessProjectAsync(projectId.Value, cancellationToken))
        {
            return Result.Failure<AiUsageSnapshotDto>("Project was not found.", 404, AiErrorCodes.PermissionDenied);
        }

        var rangeTo = rangeEnd ?? DateTimeOffset.UtcNow;
        var rangeFrom = rangeStart ?? rangeTo.AddDays(-30);
        if (rangeFrom >= rangeTo)
        {
            return Result.Failure<AiUsageSnapshotDto>("from must be before to.", 400, AiErrorCodes.InvalidRequest);
        }

        var visibleJobIds = VisibleJobs().Select(job => job.Id);
        var query = _usage.GetQueryable().AsNoTracking()
            .Where(entry => entry.CreatedAt >= rangeFrom && entry.CreatedAt < rangeTo &&
                entry.AiJobId.HasValue && visibleJobIds.Contains(entry.AiJobId.Value));
        if (projectId.HasValue) query = query.Where(entry => entry.ProjectId == projectId.Value);

        var rows = await query.ToListAsync(cancellationToken);
        return Result.Success(new AiUsageSnapshotDto(
            projectId,
            rangeFrom,
            rangeTo,
            rows.Count,
            rows.Count(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase)),
            rows.Count(entry => string.Equals(entry.Status, "failed", StringComparison.OrdinalIgnoreCase)),
            rows.Sum(entry => entry.InputTokens),
            rows.Sum(entry => entry.OutputTokens),
            rows.Sum(entry => entry.EstimatedCostUsd),
            rows.Sum(entry => entry.ActualCostUsd ?? entry.EstimatedCostUsd),
            rows.Count(entry => entry.CacheHit)));
    }

    public async Task<Result<AiBudgetSnapshotDto>> GetBudgetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue) return Result.Forbidden<AiBudgetSnapshotDto>();
        var project = await _projects.GetQueryable().AsNoTracking().FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project == null || !await CanAccessProjectAsync(projectId, cancellationToken))
        {
            return Result.Failure<AiBudgetSnapshotDto>("Project was not found.", 404, AiErrorCodes.PermissionDenied);
        }

        var policy = await _budgets.GetQueryable().AsNoTracking()
            .Where(item => item.ProjectId == projectId || (item.ProjectId == null && item.TenantId == project.OrganizationId))
            .OrderByDescending(item => item.ProjectId == projectId)
            .FirstOrDefaultAsync(cancellationToken);
        var dailyLimit = policy?.DailyBudgetUsd ?? decimal.MaxValue;
        var monthlyLimit = policy?.MonthlyBudgetUsd ?? decimal.MaxValue;
        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var costs = await _usage.GetQueryable().AsNoTracking()
            .Where(entry => entry.ProjectId == projectId && entry.CreatedAt >= startOfMonth)
            .Select(entry => new { entry.CreatedAt, Cost = entry.ActualCostUsd ?? entry.EstimatedCostUsd })
            .ToListAsync(cancellationToken);
        var dailyUsage = costs.Where(entry => entry.CreatedAt >= startOfDay).Sum(entry => entry.Cost);
        var monthlyUsage = costs.Sum(entry => entry.Cost);
        var warningPercent = policy?.WarnAtPercent ?? 80;
        var warningActive = IsThresholdReached(dailyUsage, dailyLimit, warningPercent) ||
                            IsThresholdReached(monthlyUsage, monthlyLimit, warningPercent);
        var hardStop = policy?.HardStopEnabled == true && (dailyUsage >= dailyLimit || monthlyUsage >= monthlyLimit);

        return Result.Success(new AiBudgetSnapshotDto(
            projectId,
            policy?.Id,
            dailyLimit == decimal.MaxValue ? 0 : dailyLimit,
            monthlyLimit == decimal.MaxValue ? 0 : monthlyLimit,
            warningPercent,
            policy?.HardStopEnabled ?? false,
            dailyUsage,
            monthlyUsage,
            dailyLimit == decimal.MaxValue ? 0 : Math.Max(0, dailyLimit - dailyUsage),
            monthlyLimit == decimal.MaxValue ? 0 : Math.Max(0, monthlyLimit - monthlyUsage),
            warningActive,
            hardStop));
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

    private async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        if (ProjectRoleRules.IsSystemAdmin(_currentUser.Role)) return true;
        var project = await _projects.GetQueryable().AsNoTracking().FirstOrDefaultAsync(item => item.Id == projectId, ct);
        if (project == null) return false;
        if (project.OwnerId == userId) return true;
        if (await _projectMembers.GetQueryable().AnyAsync(member => member.ProjectId == projectId && member.UserId == userId, ct)) return true;
        return project.OrganizationId.HasValue && await _organizationMembers.GetQueryable().AnyAsync(
            member => member.OrganizationId == project.OrganizationId.Value && member.UserId == userId,
            ct);
    }

    private static bool IsThresholdReached(decimal usage, decimal limit, int warningPercent)
        => limit != decimal.MaxValue && limit > 0 && usage / limit * 100 >= warningPercent;
}
