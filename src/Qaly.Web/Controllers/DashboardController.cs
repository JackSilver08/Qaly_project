using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Web.Auth;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
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
        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");

        var projectQuery = _context.Projects
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
            .AsQueryable();

        if (!isAdmin && currentUserId.HasValue)
        {
            projectQuery = projectQuery.Where(project =>
                project.OwnerId == currentUserId ||
                project.Members.Any(member => member.UserId == currentUserId));
        }

        var projects = await projectQuery
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

                var projectMembers = project.Members
                    .Select(m => new DashboardProjectMemberResponse(
                        m.UserId,
                        m.User.FullName,
                        m.Role,
                        m.User.Email))
                    .Append(new DashboardProjectMemberResponse(
                        project.OwnerId,
                        project.Owner.FullName,
                        "Owner",
                        project.Owner.Email))
                    .GroupBy(m => m.UserId)
                    .Select(g => g.First())
                    .OrderBy(m => m.FullName)
                    .ToList();

                var progressTasks = projectTasks
                    .Where(task => task.ContributesToProgress && !EqualsIgnoreCase(task.Status, "Cancelled"))
                    .ToList();
                var completedCount = progressTasks.Count(IsDone);
                var overdueCount = projectTasks.Count(task => IsOverdue(task, now));
                var progressPercentage = progressTasks.Count == 0
                    ? 0
                    : (int)Math.Round(completedCount * 100d / progressTasks.Count, MidpointRounding.AwayFromZero);

                return new DashboardProjectResponse(
                    project.Id,
                    project.Name,
                    project.Code,
                    project.Description,
                    project.LogoUrl,
                    project.Status,
                    project.OwnerId,
                    project.Owner.FullName,
                    projectMembers.Count,
                    projectTasks.Count,
                    completedCount,
                    overdueCount,
                    progressPercentage,
                    projectMembers,
                    projectTasks.Select(task =>
                    {
                        var isRestricted = IsTaskRestricted(task, project.OwnerId, currentUserId, isAdmin, projectMembers);
                        return new DashboardTaskResponse(
                            task.Id,
                            isRestricted ? $"Restricted Task #{task.Id.ToString()[..8]}" : task.Title,
                            task.Status,
                            task.Priority,
                            task.DueDate,
                            isRestricted ? null : task.Assignee?.FullName,
                            isRestricted ? string.Empty : task.Reporter.FullName,
                            project.Name,
                            task.IsPrivate,
                            isRestricted,
                            task.IsPinned,
                            task.ContributesToProgress,
                            task.UpvoteCount,
                            task.DownvoteCount,
                            isRestricted ? 0 : task.Comments.Count,
                            isRestricted ? 0 : task.Attachments.Count);
                    }).ToList(),
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
                    ? "Điều phối dự án"
                    : activeAssignments == 0
                        ? "Còn năng lực tiếp nhận"
                        : inProgressAssigned >= 2
                            ? "Luồng triển khai"
                            : "Hỗ trợ vận hành";

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
        if (currentUserId.HasValue)
        {
            var storedNotifications = await _context.Notifications
                .AsNoTracking()
                .Where(notification => notification.UserId == currentUserId.Value && !notification.IsRead)
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(6)
                .Select(notification => new DashboardNotificationResponse(
                    notification.Id.ToString(),
                    notification.Type,
                    notification.Message,
                    NotificationTone(notification.Type),
                    notification.CreatedAt))
                .ToListAsync(cancellationToken);

            notifications = storedNotifications
                .Concat(notifications)
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(6)
                .ToList();
        }

        var summary = projects.Count == 0
            ? "Không gian làm việc đã sẵn sàng. Hãy tạo dự án đầu tiên để bắt đầu theo dõi tiến độ."
            : $"Qaly đang theo dõi {activeProjects} dự án đang chạy với {completedTasks}/{allTasks.Count} công việc đã hoàn tất. " +
              $"{overdueTasks} mục quá hạn cần được chú ý, và {DisplayName(topContributor?.FullName) ?? "đội ngũ"} đang có tải công việc cao nhất.";

        var riskDigest = overdueTasks == 0 && highPriorityOpenTasks == 0
            ? "Rủi ro triển khai đang ổn định. Hãy giữ nhịp trên bảng công việc và duy trì cân bằng phân công."
            : $"{overdueTasks} công việc quá hạn và {highPriorityOpenTasks} mục ưu tiên cao đang mở đang định hình rủi ro hiện tại. " +
              "Cần rà soát hạn chót, cân bằng lại người phụ trách và gỡ các điểm nghẽn trong chu kỳ làm việc hiện tại.";

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
                    "Áp lực hạn chót",
                    $"{DisplayText(project.Name)} có {project.OverdueTaskCount} công việc quá hạn. Hãy lập lại kế hoạch cho mốc tiếp theo.",
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
                    "Thiếu người phụ trách",
                    $"{unassignedHighPriority} công việc ưu tiên cao trong {DisplayText(project.Name)} vẫn chưa có người phụ trách.",
                    "warning",
                    now.AddMinutes(-20)));
            }
        }

        if (notifications.Count == 0)
        {
            notifications.Add(new DashboardNotificationResponse(
                "workspace-ready",
                "Không gian đã đồng bộ",
                "Dữ liệu mẫu đã được nạp và bảng điều khiển đã sẵn sàng cho lập kế hoạch chu kỳ làm việc.",
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

    private static bool IsTaskRestricted(
        TaskItem task,
        Guid projectOwnerId,
        Guid? currentUserId,
        bool isAdmin,
        IReadOnlyList<DashboardProjectMemberResponse> projectMembers)
    {
        if (!task.IsPrivate || isAdmin)
        {
            return false;
        }

        if (!currentUserId.HasValue)
        {
            return true;
        }

        var isProjectManager = projectMembers.Any(member =>
            member.UserId == currentUserId.Value &&
            (EqualsIgnoreCase(member.Role, "Owner") || EqualsIgnoreCase(member.Role, "Manager") || EqualsIgnoreCase(member.Role, "Admin")));

        var canView =
            (task.ReporterId == currentUserId.Value ||
             task.AssigneeId == currentUserId.Value ||
             projectOwnerId == currentUserId.Value ||
             isProjectManager);

        return !canView;
    }

    private static string NotificationTone(string type)
        => type switch
        {
            "TaskAssigned" or "TaskStatusChanged" or "CommentAdded" => "warning",
            "DueDateReminder" => "critical",
            _ => "info"
        };

    private static int SortStatus(string? status)
        => status switch
        {
            "InProgress" => 0,
            "InReview" => 1,
            "Todo" => 2,
            "OnHold" => 3,
            "Done" => 4,
            "Cancelled" => 5,
            _ => 6
        };

    private static bool EqualsIgnoreCase(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string? DisplayName(string? value)
        => value switch
        {
            "Admin User" => "Quản trị viên",
            "Nguyen Van A" => "Nguyễn Văn A",
            "Tran Thi B" => "Trần Thị B",
            null => null,
            _ => value
        };

    private static string DisplayText(string value)
        => value switch
        {
            "Identity Hardening" => "Gia cố định danh",
            "Design Language" => "Ngôn ngữ thiết kế",
            _ => value
        };
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
    string Code,
    string? Description,
    string? LogoUrl,
    string Status,
    Guid OwnerId,
    string OwnerName,
    int MemberCount,
    int TaskCount,
    int CompletedTaskCount,
    int OverdueTaskCount,
    int ProgressPercentage,
    IReadOnlyList<DashboardProjectMemberResponse> Members,
    IReadOnlyList<DashboardTaskResponse> Tasks,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndDate);

public sealed record DashboardProjectMemberResponse(
    Guid UserId,
    string FullName,
    string Role,
    string Email);

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
    bool IsRestricted,
    bool IsPinned,
    bool ContributesToProgress,
    int UpvoteCount,
    int DownvoteCount,
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
