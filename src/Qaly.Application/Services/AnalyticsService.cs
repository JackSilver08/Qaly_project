using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Analytics;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<TimeEntry> _timeEntryRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAccessPolicy _taskAccessPolicy;

    public AnalyticsService(
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<TimeEntry> timeEntryRepo,
        ICurrentUserService currentUserService,
        ITaskAccessPolicy taskAccessPolicy)
    {
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _timeEntryRepo = timeEntryRepo;
        _currentUserService = currentUserService;
        _taskAccessPolicy = taskAccessPolicy;
    }

    public async Task<Result<ProjectAnalyticsDto>> GetProjectAnalyticsAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, ct))
        {
            return Result.Forbidden<ProjectAnalyticsDto>();
        }

        var tasks = await _taskAccessPolicy.ApplyVisibilityFilter(_taskRepo.GetQueryable())
            .Where(t => t.ProjectId == projectId)
            .ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToList();

        // TimeEntry doesn't have ProjectId — filter via TaskIds instead
        var timeEntries = await _timeEntryRepo.GetQueryable()
            .Where(te => taskIds.Contains(te.TaskId))
            .ToListAsync(ct);

        var members = await _memberRepo.GetQueryable()
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.User)
            .ToListAsync(ct);

        int totalTasks = tasks.Count;
        int doneTasks = tasks.Count(t => t.Status == "Done");
        int inProgressTasks = tasks.Count(t => t.Status == "InProgress");
        var now = DateTimeOffset.UtcNow;
        int overdueTasks = tasks.Count(t => TaskStatusRules.IsOverdue(t.Status, t.DueDate, now));

        double totalEstimatedHours = tasks.Sum(t => t.EstimatedHours ?? 0);
        double totalActualHours = timeEntries.Sum(t => t.TotalMinutes) / 60.0;

        var memberProductivity = members.Select(m => new MemberProductivityDto(
            m.UserId,
            m.User.FullName,
            tasks.Count(t => t.AssigneeId == m.UserId),
            tasks.Count(t => t.AssigneeId == m.UserId && t.Status == "Done"),
            timeEntries.Where(te => te.UserId == m.UserId).Sum(te => te.TotalMinutes) / 60.0
        )).ToList();

        // Calculate daily productivity for the last 14 days
        var startDate = DateTimeOffset.UtcNow.AddDays(-14);

        var dailyTasks = tasks
            .Where(t => t.Status == "Done" && t.UpdatedAt.HasValue && t.UpdatedAt.Value >= startDate)
            .GroupBy(t => t.UpdatedAt!.Value.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var dailyTime = timeEntries
            .Where(te => te.StartedAt >= startDate)
            .GroupBy(te => te.StartedAt.Date)
            .ToDictionary(g => g.Key, g => g.Sum(te => te.TotalMinutes) / 60.0);

        var dailyProductivity = new List<DailyProductivityDto>();
        for (int i = 0; i < 14; i++)
        {
            var date = startDate.AddDays(i).Date;
            dailyProductivity.Add(new DailyProductivityDto(
                date,
                dailyTasks.GetValueOrDefault(date, 0),
                dailyTime.GetValueOrDefault(date, 0.0)
            ));
        }

        return Result.Success(new ProjectAnalyticsDto(
            totalTasks,
            doneTasks,
            inProgressTasks,
            overdueTasks,
            Math.Round(totalEstimatedHours, 2),
            Math.Round(totalActualHours, 2),
            memberProductivity,
            dailyProductivity
        ));
    }

    public async Task<Result<WorkspaceAnalyticsDto>> GetWorkspaceAnalyticsAsync(CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden<WorkspaceAnalyticsDto>();

        var isSystemAdmin = ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);
        var allProjectIds = await _projectRepo.GetQueryable()
            .Where(project => isSystemAdmin ||
                (project.OrganizationId == null
                    ? project.OwnerId == currentUserId ||
                      project.Members.Any(member => member.UserId == currentUserId)
                    : project.Organization != null &&
                      project.Organization.IsActive &&
                      (project.Organization.OwnerId == currentUserId ||
                       project.Organization.Members.Any(member =>
                           member.UserId == currentUserId &&
                           (member.Role == OrganizationRoleRules.OrganizationAdmin ||
                            member.Role == "Admin" ||
                            member.Role == "Manager")) ||
                       ((project.OwnerId == currentUserId ||
                         project.Members.Any(member => member.UserId == currentUserId)) &&
                        project.Organization.Members.Any(member => member.UserId == currentUserId)))))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var tasks = await _taskAccessPolicy.ApplyVisibilityFilter(_taskRepo.GetQueryable())
            .Where(t => allProjectIds.Contains(t.ProjectId))
            .ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToList();

        var weekStart = DateTimeOffset.UtcNow.AddDays(-7);
        var timeEntries = await _timeEntryRepo.GetQueryable()
            .Where(te => taskIds.Contains(te.TaskId) && te.StartedAt >= weekStart)
            .ToListAsync(ct);

        return Result.Success(new WorkspaceAnalyticsDto(
            allProjectIds.Count,
            allProjectIds.Count, // All accessible projects considered active
            tasks.Count,
            tasks.Count(t => t.Status == "Done" && t.UpdatedAt.HasValue && t.UpdatedAt.Value >= weekStart),
            Math.Round(timeEntries.Sum(t => t.TotalMinutes) / 60.0, 2)
        ));
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return false;

        if (string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        var ownerId = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => (Guid?)project.OwnerId)
            .FirstOrDefaultAsync(ct);
        return ownerId.HasValue &&
            await _taskAccessPolicy.CanAccessProjectAsync(projectId, ownerId.Value, ct);
    }
}
