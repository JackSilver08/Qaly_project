using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Web.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly QalyDbContext _context;

    public DashboardController(QalyDbContext context)
    {
        _context = context;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewResponse>> GetOverview(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var projects = await _context.Projects
            .AsNoTracking()
            .AsSplitQuery()
            .Include(project => project.Owner)
            .Include(project => project.Members)
                .ThenInclude(member => member.User)
            .Include(project => project.Tasks)
                .ThenInclude(task => task.Assignee)
            .Include(project => project.Tasks)
                .ThenInclude(task => task.Reporter)
            .Include(project => project.Tasks)
                .ThenInclude(task => task.Comments)
            .Include(project => project.Tasks)
                .ThenInclude(task => task.Attachments)
            .OrderByDescending(project => project.CreatedAt)
            .ToListAsync(cancellationToken);

        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);

        var allTasks = projects
            .SelectMany(project => project.Tasks)
            .ToList();

        var activeProjects = projects.Count(project => !EqualsIgnoreCase(project.Status, "Archived"));
        var completedTasks = allTasks.Count(IsDone);
        var overdueTasks = allTasks.Count(task => IsOverdue(task, now));
        var highPriorityOpenTasks = allTasks.Count(task => !IsDone(task) && IsHighPriority(task.Priority));
        var distinctMembers = projects
            .SelectMany(project => project.Members.Select(member => member.UserId))
            .Concat(projects.Select(project => project.OwnerId))
            .Distinct()
            .Count();
        var teamMembers = Math.Max(distinctMembers, users.Count);
        var completionRate = allTasks.Count == 0
            ? 0
            : (int)Math.Round(completedTasks * 100d / allTasks.Count, MidpointRounding.AwayFromZero);

        var topContributor = users
            .Select(user => new
            {
                user.FullName,
                AssignedCount = allTasks.Count(task => task.AssigneeId == user.Id)
            })
            .OrderByDescending(item => item.AssignedCount)
            .ThenBy(item => item.FullName)
            .FirstOrDefault();

        var projectResponses = projects
            .Select(project =>
            {
                var projectTasks = project.Tasks
                    .OrderBy(task => SortStatus(task.Status))
                    .ThenBy(task => task.DueDate ?? DateTimeOffset.MaxValue)
                    .ThenBy(task => task.Title)
                    .ToList();

                var memberNames = project.Members
                    .Select(member => member.User.FullName)
                    .Append(project.Owner.FullName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList();

                var completedCount = projectTasks.Count(IsDone);
                var overdueCount = projectTasks.Count(task => IsOverdue(task, now));
                var progressPercentage = projectTasks.Count == 0
                    ? 0
                    : (int)Math.Round(completedCount * 100d / projectTasks.Count, MidpointRounding.AwayFromZero);

                return new DashboardProjectResponse(
                    project.Id,
                    project.Name,
                    project.Description,
                    project.Status,
                    project.Owner.FullName,
                    memberNames.Count,
                    projectTasks.Count,
                    completedCount,
                    overdueCount,
                    progressPercentage,
                    memberNames,
                    projectTasks.Select(task => new DashboardTaskResponse(
                        task.Id,
                        task.Title,
                        task.Status,
                        task.Priority,
                        task.DueDate,
                        task.Assignee?.FullName,
                        task.Reporter.FullName,
                        project.Name,
                        task.IsPrivate,
                        task.Comments.Count,
                        task.Attachments.Count)).ToList(),
                    project.CreatedAt,
                    project.EndDate);
            })
            .ToList();

        var teamResponses = users
            .Select(user =>
            {
                var assignedTasks = allTasks.Where(task => task.AssigneeId == user.Id).ToList();
                var completedAssigned = assignedTasks.Count(IsDone);
                var inProgressAssigned = assignedTasks.Count(task => EqualsIgnoreCase(task.Status, "InProgress"));
                var overdueAssigned = assignedTasks.Count(task => IsOverdue(task, now));
                var ownedProjects = projects.Count(project => project.OwnerId == user.Id);
                var activeAssignments = assignedTasks.Count(task => !IsDone(task));
                var capacityPercent = Math.Min(100, activeAssignments * 22 + overdueAssigned * 8);
                var focusArea = ownedProjects > 0
                    ? "Project coordination"
                    : activeAssignments == 0
                        ? "Capacity available"
                        : inProgressAssigned >= 2
                            ? "Execution stream"
                            : "Support lane";

                return new DashboardMemberResponse(
                    user.Id,
                    user.FullName,
                    user.Role,
                    user.Email,
                    user.IsActive,
                    assignedTasks.Count,
                    completedAssigned,
                    inProgressAssigned,
                    overdueAssigned,
                    capacityPercent,
                    focusArea);
            })
            .ToList();

        var notifications = BuildNotifications(projectResponses, now);

        var summary = projects.Count == 0
            ? "Workspace is ready. Create your first project to start tracking delivery."
            : $"Qaly is tracking {activeProjects} active project(s) with {completedTasks}/{allTasks.Count} tasks completed. " +
              $"{overdueTasks} overdue item(s) need attention, and {topContributor?.FullName ?? "the team"} is carrying the heaviest workload.";

        var riskDigest = overdueTasks == 0 && highPriorityOpenTasks == 0
            ? "Delivery risk is stable. Keep momentum on the active board and maintain assignment balance."
            : $"{overdueTasks} overdue task(s) and {highPriorityOpenTasks} high-priority open item(s) are shaping the current delivery risk. " +
              "Review due dates, rebalance ownership, and clear blockers in the current sprint.";

        var response = new DashboardOverviewResponse(
            now,
            new DashboardStatsResponse(
                activeProjects,
                allTasks.Count,
                overdueTasks,
                teamMembers,
                completedTasks,
                completionRate,
                highPriorityOpenTasks),
            summary,
            riskDigest,
            projectResponses,
            teamResponses,
            notifications);

        return Ok(response);
    }

    private static List<DashboardNotificationResponse> BuildNotifications(
        IReadOnlyList<DashboardProjectResponse> projects,
        DateTimeOffset now)
    {
        var notifications = new List<DashboardNotificationResponse>();

        foreach (var project in projects)
        {
            if (project.OverdueTaskCount > 0)
            {
                notifications.Add(new DashboardNotificationResponse(
                    $"{project.Id}-overdue",
                    "Deadline pressure",
                    $"{project.Name} has {project.OverdueTaskCount} overdue task(s). Replan the next sprint checkpoint.",
                    "critical",
                    now));
            }

            var unassignedHighPriority = project.Tasks.Count(task =>
                !IsDone(task.Status) &&
                IsHighPriority(task.Priority) &&
                string.IsNullOrWhiteSpace(task.AssigneeName));

            if (unassignedHighPriority > 0)
            {
                notifications.Add(new DashboardNotificationResponse(
                    $"{project.Id}-staffing",
                    "Assignment gap",
                    $"{unassignedHighPriority} high-priority task(s) in {project.Name} still have no assignee.",
                    "warning",
                    now.AddMinutes(-20)));
            }
        }

        if (notifications.Count == 0)
        {
            notifications.Add(new DashboardNotificationResponse(
                "workspace-ready",
                "Workspace synced",
                "Seed data is loaded and the dashboard is ready for sprint planning.",
                "info",
                now));
        }

        return notifications
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(6)
            .ToList();
    }

    private static bool IsDone(TaskItem task)
        => IsDone(task.Status);

    private static bool IsDone(string? status)
        => EqualsIgnoreCase(status, "Done");

    private static bool IsOverdue(TaskItem task, DateTimeOffset now)
        => task.DueDate.HasValue && task.DueDate.Value < now && !IsDone(task);

    private static bool IsHighPriority(string? priority)
        => EqualsIgnoreCase(priority, "High") || EqualsIgnoreCase(priority, "Critical");

    private static int SortStatus(string? status)
        => status switch
        {
            "InProgress" => 0,
            "Todo" => 1,
            "Done" => 2,
            _ => 3
        };

    private static bool EqualsIgnoreCase(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public sealed record DashboardOverviewResponse(
    DateTimeOffset GeneratedAt,
    DashboardStatsResponse Stats,
    string Summary,
    string RiskDigest,
    IReadOnlyList<DashboardProjectResponse> Projects,
    IReadOnlyList<DashboardMemberResponse> Team,
    IReadOnlyList<DashboardNotificationResponse> Notifications);

public sealed record DashboardStatsResponse(
    int ActiveProjects,
    int TotalTasks,
    int OverdueTasks,
    int TeamMembers,
    int CompletedTasks,
    int CompletionRate,
    int TasksAtRisk);

public sealed record DashboardProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    string OwnerName,
    int MemberCount,
    int TaskCount,
    int CompletedTaskCount,
    int OverdueTaskCount,
    int ProgressPercentage,
    IReadOnlyList<string> MemberNames,
    IReadOnlyList<DashboardTaskResponse> Tasks,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndDate);

public sealed record DashboardTaskResponse(
    Guid Id,
    string Title,
    string Status,
    string Priority,
    DateTimeOffset? DueDate,
    string? AssigneeName,
    string ReporterName,
    string ProjectName,
    bool IsPrivate,
    int CommentCount,
    int AttachmentCount);

public sealed record DashboardMemberResponse(
    Guid Id,
    string FullName,
    string Role,
    string Email,
    bool IsActive,
    int AssignedTaskCount,
    int CompletedTaskCount,
    int InProgressTaskCount,
    int OverdueTaskCount,
    int CapacityPercent,
    string FocusArea);

public sealed record DashboardNotificationResponse(
    string Id,
    string Title,
    string Message,
    string Tone,
    DateTimeOffset CreatedAt);
