using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Microsoft.Extensions.Logging;
using Qaly.Infrastructure.Data;
using Qaly.Web.Auth;
using System.Globalization;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public partial class DashboardController : BaseApiController
{
    private readonly QalyDbContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(QalyDbContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewResponse>> GetOverview(CancellationToken cancellationToken)
    {
        try
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
                    .ThenBy(task => task.SortOrder)
                    .ThenBy(task => task.DueDate ?? DateTimeOffset.MaxValue)
                    .ThenBy(task => task.Title)
                    .ToList();

                var projectMembers = project.Members
                    .Select(m => new DashboardProjectMemberResponse(
                        m.UserId,
                        m.User?.FullName ?? "Unknown member",
                        m.Role,
                        m.User?.Email ?? string.Empty,
                        m.CanViewProjectTimeline,
                        m.CanViewTaskRisk,
                        m.CanNudgeAssignee,
                        m.CanViewUnseenTaskSignal))
                    .Append(new DashboardProjectMemberResponse(
                        project.OwnerId,
                        project.Owner?.FullName ?? "Unknown owner",
                        "Owner",
                        project.Owner?.Email ?? string.Empty,
                        true,
                        true,
                        true,
                        true))
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
                        project.Owner?.FullName ?? "Unknown owner",
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
                            isRestricted ? null : task.AssigneeId,
                            isRestricted ? null : task.Assignee?.FullName,
                            isRestricted ? string.Empty : (task.Reporter?.FullName ?? string.Empty),
                            project.Name,
                            task.SortOrder,
                            ToRowVersion(task.RowVersion),
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
        catch (Exception ex)
        {
            LogFailedToBuildDashboardOverview(_logger, ex);
            return Ok(CreateSafeFallbackOverview(DateTimeOffset.UtcNow));
        }
    }

    private static DashboardOverviewResponse CreateSafeFallbackOverview(DateTimeOffset now)
        => new(
            now,
            new DashboardStatsResponse(0, 0, 0, 0, 0, 0, 0),
            "Không thể tải số liệu trực tiếp, nên hệ thống đang hiển thị dữ liệu an toàn.",
            "Đã gặp lỗi khi tổng hợp dashboard; vui lòng kiểm tra dữ liệu dự án hoặc nhật ký máy chủ.",
            Array.Empty<DashboardProjectResponse>(),
            Array.Empty<DashboardMemberResponse>(),
            [
                new DashboardNotificationResponse(
                    "dashboard-fallback",
                    "Đang dùng dữ liệu an toàn",
                    "Dashboard đã chuyển sang dữ liệu an toàn để tránh màn hình lỗi.",
                    "warning",
                    now)
            ]);

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

    private static string ToRowVersion(byte[]? rowVersion)
        => rowVersion is { Length: > 0 } ? Convert.ToBase64String(rowVersion) : string.Empty;

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

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Failed to build dashboard overview. Returning safe fallback response.")]
    private static partial void LogFailedToBuildDashboardOverview(ILogger logger, Exception exception);

    [HttpGet("attention-summary")]
    public async Task<ActionResult<AttentionSummaryDto>> GetAttentionSummary(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");

        var projectQuery = _context.Projects.AsNoTracking().Include(p => p.Tasks).AsQueryable();

        if (!isAdmin && currentUserId.HasValue)
        {
            projectQuery = projectQuery.Where(p =>
                p.OwnerId == currentUserId ||
                p.Members.Any(m => m.UserId == currentUserId));
        }

        var projects = await projectQuery.ToListAsync(cancellationToken);
        var allTasks = projects.SelectMany(p => p.Tasks).ToList();

        var overdueTasksCount = allTasks.Count(t => IsOverdue(t, now));
        var dueSoonTasksCount = allTasks.Count(t => !IsDone(t) && t.DueDate.HasValue && t.DueDate.Value > now && (t.DueDate.Value - now).TotalHours <= 48);
        var blockedTasksCount = allTasks.Count(t => EqualsIgnoreCase(t.Status, "Blocked") || EqualsIgnoreCase(t.Status, "OnHold"));
        
        var riskProjectsCount = projects.Count(p => 
        {
            var pTasks = p.Tasks.Where(t => t.ContributesToProgress && !EqualsIgnoreCase(t.Status, "Cancelled")).ToList();
            if (pTasks.Count == 0) return false;
            var completedCount = pTasks.Count(IsDone);
            var overdueCount = pTasks.Count(t => IsOverdue(t, now));
            var progress = (int)Math.Round(completedCount * 100d / pTasks.Count, MidpointRounding.AwayFromZero);
            return progress < 40 || overdueCount > 2;
        });

        var safeProjectsCount = projects.Count(p => 
        {
            var pTasks = p.Tasks.Where(t => t.ContributesToProgress && !EqualsIgnoreCase(t.Status, "Cancelled")).ToList();
            if (pTasks.Count == 0) return true;
            var completedCount = pTasks.Count(IsDone);
            var overdueCount = pTasks.Count(t => IsOverdue(t, now));
            var progress = (int)Math.Round(completedCount * 100d / pTasks.Count, MidpointRounding.AwayFromZero);
            return overdueCount == 0 && progress >= 70;
        });

        var totalItems = overdueTasksCount + dueSoonTasksCount + riskProjectsCount;

        return Ok(new AttentionSummaryDto(
            overdueTasksCount,
            dueSoonTasksCount,
            riskProjectsCount,
            blockedTasksCount,
            safeProjectsCount,
            totalItems
        ));
    }

    [HttpGet("recent-activities")]
    public async Task<ActionResult<RecentActivitiesResponseDto>> GetRecentActivities(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfToday = now.Date;
        var startOfWeek = now.Date.AddDays(-7);

        var query = _context.AuditLogs.AsNoTracking().Where(a => a.Timestamp >= startOfWeek);

        var logs = await query.OrderByDescending(a => a.Timestamp).ToListAsync(cancellationToken);

        var todayCount = logs.Count(l => l.Timestamp >= startOfToday);
        var weekCount = logs.Count;

        var activityByDay = Enumerable.Range(0, 7)
            .Select(i => 
            {
                var d = startOfWeek.AddDays(i);
                return new ActivityByDayDto(
                    d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    logs.Count(l => l.Timestamp.Date == d.Date)
                );
            })
            .ToList();

        var latestActivities = logs.Take(3).Select(l => 
        {
            string? projectName = null;
            string title = l.Action + " " + l.EntityType;
            try {
                if (!string.IsNullOrEmpty(l.ChangesJson)) {
                    var changes = System.Text.Json.JsonDocument.Parse(l.ChangesJson);
                    if (changes.RootElement.TryGetProperty("title", out var tProp) || changes.RootElement.TryGetProperty("Title", out tProp)) {
                        title = l.Action switch {
                            "Create" => "Tạo mới " + tProp.GetString(),
                            "Update" => "Cập nhật " + tProp.GetString(),
                            "StatusChange" => "Đổi trạng thái " + tProp.GetString(),
                            _ => l.Action + " " + tProp.GetString()
                        };
                    }
                }
            } catch {}

            return new RecentActivityDto(
                l.Action,
                title,
                l.User?.FullName ?? "Hệ thống",
                projectName,
                l.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
            );
        }).ToList();

        return Ok(new RecentActivitiesResponseDto(
            todayCount,
            weekCount,
            activityByDay,
            latestActivities
        ));
    }

    [HttpGet("strategic-overview")]
    public async Task<ActionResult<StrategicOverviewDto>> GetStrategicOverview(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");

        var projectQuery = _context.Projects.AsNoTracking().Include(p => p.Tasks).AsQueryable();

        if (!isAdmin && currentUserId.HasValue)
        {
            projectQuery = projectQuery.Where(p =>
                p.OwnerId == currentUserId ||
                p.Members.Any(m => m.UserId == currentUserId));
        }

        var projects = await projectQuery.ToListAsync(cancellationToken);
        var allTasks = projects.SelectMany(p => p.Tasks).ToList();

        var activeProjects = projects.Count(p => !EqualsIgnoreCase(p.Status, "Archived"));
        var completedTasks = allTasks.Count(IsDone);
        var overdueTasks = allTasks.Count(t => IsOverdue(t, now));
        var dueSoonTasks = allTasks.Count(t => !IsDone(t) && t.DueDate.HasValue && t.DueDate.Value > now && (t.DueDate.Value - now).TotalHours <= 48);
        
        var completionRate = allTasks.Count == 0 ? 0 : (int)Math.Round(completedTasks * 100d / allTasks.Count, MidpointRounding.AwayFromZero);
        var averageProjectProgress = projects.Count == 0 ? 0 : (int)projects.Average(p => 
        {
            var pTasks = p.Tasks.Where(t => t.ContributesToProgress && !EqualsIgnoreCase(t.Status, "Cancelled")).ToList();
            if (pTasks.Count == 0) return 0;
            return Math.Round(pTasks.Count(IsDone) * 100d / pTasks.Count, MidpointRounding.AwayFromZero);
        });

        var riskProjectCount = projects.Count(p => 
        {
            var pTasks = p.Tasks.Where(t => t.ContributesToProgress && !EqualsIgnoreCase(t.Status, "Cancelled")).ToList();
            if (pTasks.Count == 0) return false;
            var comp = pTasks.Count(IsDone);
            var prog = Math.Round(comp * 100d / pTasks.Count, MidpointRounding.AwayFromZero);
            return prog < 40 || pTasks.Count(t => IsOverdue(t, now)) > 2;
        });

        var teamWorkloadLevel = (allTasks.Count(t => !IsDone(t)) / (double)Math.Max(1, _context.Users.Count())) > 5 ? "High" : "Medium";
        var riskLevel = overdueTasks > 5 || riskProjectCount > 1 ? "High" : (overdueTasks > 0 ? "Moderate" : "Low");

        var topPriorityTasks = allTasks
            .Where(t => !IsDone(t) && (IsHighPriority(t.Priority) || IsOverdue(t, now) || (t.DueDate.HasValue && (t.DueDate.Value - now).TotalHours <= 48)))
            .OrderBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
            .Take(3)
            .Select(t => new DashboardTaskResponse(
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.DueDate,
                t.AssigneeId,
                t.Assignee?.FullName,
                t.Reporter?.FullName ?? string.Empty,
                projects.FirstOrDefault(p => p.Id == t.ProjectId)?.Name ?? "",
                t.SortOrder,
                ToRowVersion(t.RowVersion),
                t.IsPrivate,
                false,
                t.IsPinned,
                t.ContributesToProgress,
                t.UpvoteCount,
                t.DownvoteCount,
                0,
                0
            ))
            .ToList();

        return Ok(new StrategicOverviewDto(
            averageProjectProgress,
            averageProjectProgress,
            completionRate,
            riskProjectCount,
            overdueTasks,
            dueSoonTasks,
            activeProjects,
            teamWorkloadLevel,
            riskLevel,
            topPriorityTasks
        ));
    }

    [HttpPost("ai-strategy")]
    public ActionResult<AiStrategyResponseDto> GenerateAiStrategy([FromBody] StrategicOverviewDto data)
    {
        // Rule-based fallback implementation
        var riskAnalysis = new List<string>();
        var recommendations = new List<string>();
        var priorityPlan = new List<string>();
        string summary = "Workspace đang vận hành ổn định nhưng cần duy trì nhịp độ triển khai.";

        if (data.OverdueTaskCount > 0)
        {
            riskAnalysis.Add($"Có {data.OverdueTaskCount} task đã trễ hạn.");
            recommendations.Add("Ưu tiên xử lý ngay các task đang trễ hạn.");
        }
        
        if (data.DueSoonTaskCount > 0)
        {
            riskAnalysis.Add($"Có {data.DueSoonTaskCount} task sắp đến hạn trong 48h tới.");
            recommendations.Add("Hoàn thành các task sắp đến hạn để tránh tồn đọng.");
        }

        if (data.RiskProjectCount > 0)
        {
            riskAnalysis.Add($"{data.RiskProjectCount} dự án có tiến độ thấp hơn mức kỳ vọng hoặc có nhiều task quá hạn.");
            summary = "Workspace đang có dấu hiệu rủi ro do một số dự án và nhiệm vụ chậm tiến độ.";
            recommendations.Add("Cần họp review lại timeline cho các dự án rủi ro và điều chỉnh scope.");
        }

        if (data.TeamWorkloadLevel == "High")
        {
            riskAnalysis.Add("Một số thành viên có workload khá cao.");
            recommendations.Add("Điều phối lại workload cho các thành viên đang quá tải, tạm dừng các task priority thấp.");
        }

        if (riskAnalysis.Count == 0)
        {
            riskAnalysis.Add("Tất cả dự án đang đúng tiến độ.");
            recommendations.Add("Tiếp tục duy trì hiệu suất làm việc hiện tại.");
        }

        foreach (var task in data.TopPriorityTasks.Take(3))
        {
            priorityPlan.Add($"Tập trung: {task.Title} ({task.ProjectName})");
        }
        
        if (priorityPlan.Count == 0)
        {
            priorityPlan.Add("Rà soát backlog");
            priorityPlan.Add("Lập kế hoạch cho Sprint tiếp theo");
        }

        return Ok(new AiStrategyResponseDto(
            summary,
            riskAnalysis,
            recommendations,
            priorityPlan
        ));
    }
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
    string Email,
    bool CanViewProjectTimeline,
    bool CanViewTaskRisk,
    bool CanNudgeAssignee,
    bool CanViewUnseenTaskSignal);

public sealed record DashboardTaskResponse(
    Guid Id,
    string Title,
    string Status,
    string Priority,
    DateTimeOffset? DueDate,
    Guid? AssigneeId,
    string? AssigneeName,
    string ReporterName,
    string ProjectName,
    int SortOrder,
    string RowVersion,
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

public sealed record AttentionSummaryDto(
    int OverdueTasks,
    int DueSoonTasks,
    int RiskProjects,
    int BlockedTasks,
    int SafeProjects,
    int TotalAttentionItems);

public sealed record ActivityByDayDto(
    string Date,
    int Count);

public sealed record RecentActivityDto(
    string Type,
    string Title,
    string ActorName,
    string? ProjectName,
    string CreatedAt);

public sealed record RecentActivitiesResponseDto(
    int TodayCount,
    int WeekCount,
    IReadOnlyList<ActivityByDayDto> ActivityByDay,
    IReadOnlyList<RecentActivityDto> LatestActivities);

public sealed record StrategicOverviewDto(
    int WorkspaceHealthScore,
    int AverageProjectProgress,
    int TaskCompletionRate,
    int RiskProjectCount,
    int OverdueTaskCount,
    int DueSoonTaskCount,
    int ActiveProjectCount,
    string TeamWorkloadLevel,
    string RiskLevel,
    IReadOnlyList<DashboardTaskResponse> TopPriorityTasks);

public sealed record AiStrategyResponseDto(
    string Summary,
    IReadOnlyList<string> RiskAnalysis,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> PriorityPlan);

