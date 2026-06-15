using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Dashboard;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;

namespace Qaly.Application.Services;

public sealed class DashboardSummaryService : IDashboardSummaryService
{
    private static readonly string[] OpenStatuses = ["Todo", "InProgress", "InReview", "OnHold"];

    private readonly IProjectDashboardSummaryRepository _repository;
    private readonly ITaskAccessPolicy _taskAccessPolicy;

    public DashboardSummaryService(
        IProjectDashboardSummaryRepository repository,
        ITaskAccessPolicy taskAccessPolicy)
    {
        _repository = repository;
        _taskAccessPolicy = taskAccessPolicy;
    }

    public async Task<Result<ProjectDashboardSummaryDto>> GetProjectSummaryAsync(
        Guid projectId,
        DateTimeOffset? from = null,
        DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        if (from.HasValue && toDate.HasValue && from.Value > toDate.Value)
        {
            return Result.Failure<ProjectDashboardSummaryDto>("'from' must be less than or equal to 'to'.", 400);
        }

        var projectInfo = await _repository.GetProjectInfoAsync(projectId, ct);
        if (projectInfo == null)
        {
            return Result.NotFound<ProjectDashboardSummaryDto>("Không tìm thấy dự án.");
        }

        if (!await _taskAccessPolicy.CanAccessProjectAsync(projectId, projectInfo.OwnerId, ct))
        {
            return Result.Forbidden<ProjectDashboardSummaryDto>();
        }

        var now = DateTimeOffset.UtcNow;
        var dueSoonThreshold = now.AddHours(24);

        var query = _taskAccessPolicy.ApplyVisibilityFilter(
                _repository.QueryProjectTasks(projectId, from, toDate))
            .AsNoTracking();

        var aggregate = await query
            .GroupBy(_ => 1)
            .Select(group => new MetricAggregate
            {
                TotalTasks = group.Count(),
                OpenTasks = group.Count(task => OpenStatuses.Contains(task.Status)),
                BacklogTasks = group.Count(task => task.Status == "Todo"),
                InProgressTasks = group.Count(task => task.Status == "InProgress"),
                DoneTasks = group.Count(task => task.Status == "Done"),
                CancelledTasks = group.Count(task => task.Status == "Cancelled"),
                OverdueTasks = group.Count(task =>
                    OpenStatuses.Contains(task.Status) &&
                    task.DueDate.HasValue &&
                    task.DueDate.Value < now),
                DueSoon24h = group.Count(task =>
                    OpenStatuses.Contains(task.Status) &&
                    task.DueDate.HasValue &&
                    task.DueDate.Value >= now &&
                    task.DueDate.Value < dueSoonThreshold),
                CompletedForProgress = group.Count(task =>
                    task.ContributesToProgress &&
                    task.Status == "Done"),
                TotalForProgress = group.Count(task =>
                    task.ContributesToProgress &&
                    task.Status != "Cancelled"),
                MissingDueDateOpen = group.Count(task =>
                    OpenStatuses.Contains(task.Status) &&
                    task.DueDate == null),
                MissingAssigneeOpen = group.Count(task =>
                    OpenStatuses.Contains(task.Status) &&
                    task.AssigneeId == null &&
                    task.Assignees.Count == 0)
            })
            .FirstOrDefaultAsync(ct) ?? new MetricAggregate();

        var completionRate = aggregate.TotalForProgress == 0
            ? 0m
            : Math.Round(aggregate.CompletedForProgress * 100m / aggregate.TotalForProgress, 2, MidpointRounding.AwayFromZero);

        var rawStatusBreakdown = await query
            .GroupBy(task => task.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(ct);

        var statusBreakdown = rawStatusBreakdown
            .Select(item => new ProjectDashboardStatusBreakdownDto(
                string.IsNullOrWhiteSpace(item.Status) ? "Không xác định" : item.Status!,
                item.Count))
            .OrderByDescending(item => item.Count)
            .ToList();

        var response = new ProjectDashboardSummaryDto(
            now,
            projectId,
            from,
            toDate,
            new ProjectDashboardSummaryMetricsDto(
                aggregate.TotalTasks,
                aggregate.OpenTasks,
                aggregate.BacklogTasks,
                aggregate.InProgressTasks,
                aggregate.DoneTasks,
                aggregate.CancelledTasks,
                completionRate,
                aggregate.OverdueTasks,
                aggregate.DueSoon24h),
            statusBreakdown,
            new ProjectDashboardDataQualityDto(
                aggregate.MissingDueDateOpen,
                aggregate.MissingAssigneeOpen));

        return Result.Success(response);
    }

    private sealed class MetricAggregate
    {
        public int TotalTasks { get; init; }
        public int OpenTasks { get; init; }
        public int BacklogTasks { get; init; }
        public int InProgressTasks { get; init; }
        public int DoneTasks { get; init; }
        public int CancelledTasks { get; init; }
        public int OverdueTasks { get; init; }
        public int DueSoon24h { get; init; }
        public int CompletedForProgress { get; init; }
        public int TotalForProgress { get; init; }
        public int MissingDueDateOpen { get; init; }
        public int MissingAssigneeOpen { get; init; }
    }
}
