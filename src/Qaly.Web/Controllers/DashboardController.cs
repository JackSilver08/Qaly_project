using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Microsoft.Extensions.Logging;
using Qaly.Infrastructure.Data;
using Qaly.Web.Auth;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using System.Globalization;
using System.Text.Json;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public partial class DashboardController : BaseApiController
{
    private static readonly JsonSerializerOptions WebJsonSerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly QalyDbContext _context;
    private readonly ILogger<DashboardController> _logger;
    private readonly IAiGateway _aiGateway;
    private readonly ITaskAccessPolicy _taskAccessPolicy;

    public DashboardController(
        QalyDbContext context,
        ILogger<DashboardController> logger,
        IAiGateway aiGateway,
        ITaskAccessPolicy taskAccessPolicy)
    {
        _context = context;
        _logger = logger;
        _aiGateway = aiGateway;
        _taskAccessPolicy = taskAccessPolicy;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewResponse>> GetOverview(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var currentUserId = User.GetUserId();
            var isAdmin = User.IsInRole("Admin");

            IQueryable<Project> BuildProjectQuery(bool restrictToMembership)
            {
                var query = _context.Projects
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(project => project.Owner)
                    .Include(project => project.Organization)
                        .ThenInclude(organization => organization!.Members)
                    .Include(project => project.Members)
                        .ThenInclude(member => member.User)
                    .Include(project => project.Tasks)
                        .ThenInclude(task => task.Assignee)
                    .Include(project => project.Tasks)
                        .ThenInclude(task => task.Assignees)
                    .Include(project => project.Tasks)
                        .ThenInclude(task => task.Reporter)
                    .Include(project => project.Tasks)
                        .ThenInclude(task => task.Comments)
                    .Include(project => project.Tasks)
                        .ThenInclude(task => task.Attachments)
                    .AsQueryable();

                if (restrictToMembership)
                {
                    query = ApplyProjectVisibility(query, currentUserId, isAdmin);
                }

                return query;
            }

            var projects = await BuildProjectQuery(restrictToMembership: true)
                .OrderByDescending(project => project.CreatedAt)
                .ToListAsync(cancellationToken);

            FilterPrivateTasks(projects, currentUserId, isAdmin);

            var accessibleUserIds = projects
                .SelectMany(project => project.Members.Select(member => member.UserId))
                .Concat(projects.Select(project => project.OwnerId))
                .Append(currentUserId ?? Guid.Empty)
                .Distinct()
                .ToList();

            var usersQuery = _context.Users
                .AsNoTracking()
                .Where(user => isAdmin || accessibleUserIds.Contains(user.Id))
                .OrderBy(user => user.FullName);

            var users = await usersQuery.ToListAsync(cancellationToken);

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

        var organizationIds = projects.Select(project => project.OrganizationId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var customRoleRows = await _context.ProjectRoleDefinitions.AsNoTracking()
            .Where(role => organizationIds.Contains(role.OrganizationId))
            .Select(role => new { role.OrganizationId, role.Key, role.DisplayName, role.BaseRole })
            .ToListAsync(cancellationToken);
        var customRoles = customRoleRows.ToDictionary(
            role => $"{role.OrganizationId:N}:{ProjectRoleCatalog.NormalizeKey(role.Key)}",
            role => (role.DisplayName, BaseRole: ProjectRoleRules.NormalizeProjectRole(role.BaseRole)),
            StringComparer.Ordinal);

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
                var storedRole = projectMembers.FirstOrDefault(member => member.UserId == currentUserId)?.Role;
                var effectiveRole = storedRole;
                string? customRoleLabel = null;
                if (project.OrganizationId.HasValue && !string.IsNullOrWhiteSpace(storedRole) &&
                    customRoles.TryGetValue(
                        $"{project.OrganizationId.Value:N}:{ProjectRoleCatalog.NormalizeKey(storedRole)}",
                        out var customRole))
                {
                    effectiveRole = customRole.BaseRole;
                    customRoleLabel = customRole.DisplayName;
                }
                var permissions = ProjectPermissionRules.Resolve(
                    effectiveRole,
                    isOwner: currentUserId.HasValue && project.OwnerId == currentUserId.Value,
                    isSystemAdmin: isAdmin,
                    isOrganizationManager: currentUserId.HasValue &&
                        project.Organization != null &&
                        project.Organization.IsActive &&
                        (project.Organization.OwnerId == currentUserId.Value ||
                         OrganizationRoleRules.CanManageOrganization(
                             project.Organization.Members
                                 .FirstOrDefault(member => member.UserId == currentUserId.Value)?.Role)));
                if (customRoleLabel != null)
                {
                    permissions = permissions with { Role = storedRole!, RoleLabel = customRoleLabel };
                }

                return new DashboardProjectResponse(
                        project.Id,
                        project.Name,
                        project.Code,
                        project.Description,
                        project.LogoUrl,
                        project.Status,
                        project.OrganizationId,
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
                            isRestricted ? null : task.Description,
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
                            isRestricted ? 0 : task.Attachments.Count,
                            task.SprintId);
                    }).ToList(),
                    project.CreatedAt,
                    project.EndDate,
                    project.EnableOnHold,
                    project.EnableInReview,
                    project.RequireEvidenceToDone,
                    project.RestrictTransitionsToAdmin,
                    permissions);
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
                .ThenByDescending(notification => notification.Id)
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
                .ThenBy(notification => notification.Id)
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
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Không thể tải dữ liệu dashboard lúc này.");
        }
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

    private static IQueryable<Project> ApplyProjectVisibility(
        IQueryable<Project> query,
        Guid? currentUserId,
        bool isSystemAdmin)
    {
        if (isSystemAdmin)
        {
            return query;
        }

        if (!currentUserId.HasValue)
        {
            return query.Where(project => false);
        }

        var userId = currentUserId.Value;
        return query.Where(project =>
            project.OrganizationId == null
                ? project.OwnerId == userId ||
                  project.Members.Any(member => member.UserId == userId)
                : project.Organization != null &&
                  project.Organization.IsActive &&
                  (project.Organization.OwnerId == userId ||
                   project.Organization.Members.Any(member =>
                       member.UserId == userId &&
                       (member.Role == OrganizationRoleRules.OrganizationAdmin ||
                        member.Role == "Admin" ||
                        member.Role == "Manager")) ||
                   ((project.OwnerId == userId ||
                     project.Members.Any(member => member.UserId == userId)) &&
                    project.Organization.Members.Any(member => member.UserId == userId))));
    }

    private static void FilterPrivateTasks(
        IEnumerable<Project> projects,
        Guid? currentUserId,
        bool isSystemAdmin)
    {
        if (isSystemAdmin)
        {
            return;
        }

        foreach (var project in projects)
        {
            project.Tasks = currentUserId.HasValue
                ? project.Tasks.Where(task =>
                    !task.IsPrivate ||
                    task.ReporterId == currentUserId.Value ||
                    task.AssigneeId == currentUserId.Value ||
                    task.Assignees.Any(assignment => assignment.UserId == currentUserId.Value) ||
                    project.OwnerId == currentUserId.Value).ToList()
                : [];
        }
    }

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

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Failed to build dashboard overview.")]
    private static partial void LogFailedToBuildDashboardOverview(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "AI provider {Provider} returned an invalid workspace strategy payload.")]
    private static partial void LogInvalidWorkspaceStrategyPayload(ILogger logger, string? provider);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Debug, Message = "Audit log {AuditLogId} contains malformed ChangesJson; dashboard uses the safe fallback title.")]
    private static partial void LogMalformedAuditChanges(ILogger logger, long auditLogId, Exception exception);

    [HttpGet("attention-summary")]
    public async Task<ActionResult<AttentionSummaryDto>> GetAttentionSummary(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");

        var projectQuery = ApplyProjectVisibility(
            _context.Projects.AsNoTracking()
                .Include(project => project.Tasks)
                    .ThenInclude(task => task.Assignees)
                .AsQueryable(),
            currentUserId,
            isAdmin);

        var projects = await projectQuery.ToListAsync(cancellationToken);
        FilterPrivateTasks(projects, currentUserId, isAdmin);
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

        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");

        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.Timestamp >= startOfWeek);

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Take(200)
            .ToListAsync(cancellationToken);

        var visibleLogs = new List<AuditLog>();
        foreach (var log in logs)
        {
            if (await CanSeeAuditLogAsync(log, currentUserId, isAdmin, cancellationToken))
            {
                visibleLogs.Add(log);
            }
        }

        var todayCount = visibleLogs.Count(l => l.Timestamp >= startOfToday);
        var weekCount = visibleLogs.Count;

        var activityByDay = Enumerable.Range(0, 7)
            .Select(i => 
            {
                var d = startOfWeek.AddDays(i);
                return new ActivityByDayDto(
                    d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    visibleLogs.Count(l => l.Timestamp.Date == d.Date)
                );
            })
            .ToList();

        // P0-fix: Build projectNames only from projects referenced in the VISIBLE (tenant-scoped) logs.
        // Loading all project names would leak project identity across tenant boundaries.
        var visibleProjectIds = visibleLogs
            .Select(l => InferProjectId(l))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var projectNames = await _context.Projects
            .AsNoTracking()
            .Where(project => visibleProjectIds.Contains(project.Id))
            .Select(project => new { project.Id, project.Name })
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);

        var latestActivities = visibleLogs.Take(3).Select(l => 
        {
            var projectId = InferProjectId(l);
            string? projectName = null;
            if (projectId.HasValue && projectNames.TryGetValue(projectId.Value, out var resolvedProjectName))
            {
                projectName = resolvedProjectName;
            }
            string title = l.Action + " " + l.EntityType;
            try {
                if (!string.IsNullOrEmpty(l.ChangesJson)) {
                    using var changes = JsonDocument.Parse(l.ChangesJson);
                    if (changes.RootElement.TryGetProperty("title", out var tProp) || changes.RootElement.TryGetProperty("Title", out tProp)) {
                        title = l.Action switch {
                            "Create" => "Tạo mới " + tProp.GetString(),
                            "Update" => "Cập nhật " + tProp.GetString(),
                            "StatusChange" => "Đổi trạng thái " + tProp.GetString(),
                            _ => l.Action + " " + tProp.GetString()
                        };
                    }
                }
            }
            catch (JsonException exception)
            {
                LogMalformedAuditChanges(_logger, l.Id, exception);
            }

            return new RecentActivityDto(
                l.Action,
                title,
                l.User?.FullName ?? "Hệ thống",
                projectName,
                projectId,
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

    private Guid? InferProjectId(AuditLog log)
    {
        if (log.EntityType == nameof(Project) && Guid.TryParse(log.EntityId, out var projectId))
        {
            return projectId;
        }

        if (!string.IsNullOrWhiteSpace(log.ChangesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(log.ChangesJson);
                var root = doc.RootElement;

                if (TryReadGuid(root, "projectId", out var parsedProjectId) || TryReadGuid(root, "ProjectId", out parsedProjectId))
                {
                    return parsedProjectId;
                }

                if (log.EntityType == nameof(TaskComment) || log.EntityType == nameof(TaskAttachment))
                {
                    if (TryReadGuid(root, "taskItemId", out var taskItemId) || TryReadGuid(root, "TaskItemId", out taskItemId))
                    {
                        return _context.TaskItems.AsNoTracking()
                            .Where(task => task.Id == taskItemId)
                            .Select(task => (Guid?)task.ProjectId)
                            .FirstOrDefault();
                    }
                }

                if (log.EntityType == nameof(Sprint))
                {
                    if (TryReadGuid(root, "sprintId", out var sprintId) || TryReadGuid(root, "SprintId", out sprintId))
                    {
                        return _context.TaskItems.AsNoTracking()
                            .Where(task => task.SprintId == sprintId)
                            .Select(task => (Guid?)task.ProjectId)
                            .FirstOrDefault();
                    }
                }
            }
            catch
            {
            }
        }

        if (log.EntityType == nameof(TaskItem) && Guid.TryParse(log.EntityId, out var taskId))
        {
            return _context.TaskItems.AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => (Guid?)task.ProjectId)
                .FirstOrDefault();
        }

        return null;
    }

    private static bool TryReadGuid(JsonElement root, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind == JsonValueKind.String && Guid.TryParse(property.GetString(), out value);
    }

    private async Task<bool> CanSeeAuditLogAsync(
        AuditLog log,
        Guid? currentUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        if (isAdmin)
        {
            return true;
        }

        if (!currentUserId.HasValue)
        {
            return false;
        }

        var taskId = TryResolveTaskId(log);
        if (taskId.HasValue)
        {
            var task = await _context.TaskItems.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(item => item.Project)
                    .ThenInclude(project => project.Organization)
                .Include(item => item.Assignees)
                .SingleOrDefaultAsync(item => item.Id == taskId.Value, ct);
            return task != null && await _taskAccessPolicy.CanAccessTaskAsync(task, ct);
        }

        if (log.UserId == currentUserId.Value)
        {
            return true;
        }

        var projectId = await TryResolveProjectIdAsync(log, ct);
        if (!projectId.HasValue)
        {
            return false;
        }

        return await CanAccessProjectAsync(projectId.Value, currentUserId.Value, ct);
    }

    private static Guid? TryResolveTaskId(AuditLog log)
    {
        if (log.EntityType == nameof(TaskItem) && Guid.TryParse(log.EntityId, out var entityTaskId))
        {
            return entityTaskId;
        }

        if (log.EntityType is not (nameof(TaskComment) or nameof(TaskAttachment)) ||
            string.IsNullOrWhiteSpace(log.ChangesJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(log.ChangesJson);
            return TryReadGuid(document.RootElement, "taskItemId", out var taskId) ||
                TryReadGuid(document.RootElement, "TaskItemId", out taskId)
                ? taskId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, Guid currentUserId, CancellationToken ct)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .Include(item => item.Organization)
                .ThenInclude(item => item!.Members)
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == projectId, ct);

        if (project == null)
        {
            return false;
        }

        if (project.Organization != null)
        {
            if (!project.Organization.IsActive)
            {
                return false;
            }

            var organizationRole = project.Organization.Members
                .FirstOrDefault(member => member.UserId == currentUserId)?.Role;
            if (project.Organization.OwnerId == currentUserId ||
                OrganizationRoleRules.CanManageOrganization(organizationRole))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(organizationRole))
            {
                return false;
            }
        }

        if (project.OwnerId == currentUserId)
        {
            return true;
        }

        if (project.Members.Any(member => member.UserId == currentUserId))
        {
            return true;
        }

        return false;
    }

    private async Task<Guid?> TryResolveProjectIdAsync(AuditLog log, CancellationToken ct)
    {
        if (log.EntityType == nameof(Project) && Guid.TryParse(log.EntityId, out var projectId))
        {
            return projectId;
        }

        if (!string.IsNullOrWhiteSpace(log.ChangesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(log.ChangesJson);
                var root = doc.RootElement;

                if (TryReadGuid(root, "projectId", out var parsedProjectId) || TryReadGuid(root, "ProjectId", out parsedProjectId))
                {
                    return parsedProjectId;
                }

                if (log.EntityType == nameof(TaskComment) || log.EntityType == nameof(TaskAttachment))
                {
                    if (TryReadGuid(root, "taskItemId", out var taskItemId) || TryReadGuid(root, "TaskItemId", out taskItemId))
                    {
                        return await _context.TaskItems
                            .AsNoTracking()
                            .Where(task => task.Id == taskItemId)
                            .Select(task => (Guid?)task.ProjectId)
                            .FirstOrDefaultAsync(ct);
                    }
                }

                if (log.EntityType == nameof(Sprint))
                {
                    if (TryReadGuid(root, "sprintId", out var sprintId) || TryReadGuid(root, "SprintId", out sprintId))
                    {
                        return await _context.TaskItems
                            .AsNoTracking()
                            .Where(task => task.SprintId == sprintId)
                            .Select(task => (Guid?)task.ProjectId)
                            .FirstOrDefaultAsync(ct);
                    }
                }
            }
            catch
            {
            }
        }

        if (log.EntityType == nameof(TaskItem) && Guid.TryParse(log.EntityId, out var taskId))
        {
            return await _context.TaskItems
                .AsNoTracking()
                .Where(task => task.Id == taskId)
                .Select(task => (Guid?)task.ProjectId)
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    [HttpGet("strategic-overview")]
    public async Task<ActionResult<StrategicOverviewDto>> GetStrategicOverview(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");

        var projectQuery = ApplyProjectVisibility(
            _context.Projects.AsNoTracking()
                .Include(project => project.Members)
                .Include(project => project.Tasks)
                    .ThenInclude(task => task.Assignees)
                .AsQueryable(),
            currentUserId,
            isAdmin);

        var projects = await projectQuery.ToListAsync(cancellationToken);
        FilterPrivateTasks(projects, currentUserId, isAdmin);
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

        // P0-fix: Count only users who participate in accessible projects (project members + owners),
        // not all users in the database, which would leak cross-tenant user counts.
        var accessibleUserCount = projects
            .SelectMany(p => p.Members.Select(m => m.UserId))
            .Concat(projects.Select(p => p.OwnerId))
            .Distinct()
            .Count();
        var teamWorkloadLevel = (allTasks.Count(t => !IsDone(t)) / (double)Math.Max(1, accessibleUserCount)) > 5 ? "High" : "Medium";
        var riskLevel = overdueTasks > 5 || riskProjectCount > 1 ? "High" : (overdueTasks > 0 ? "Moderate" : "Low");

        var topPriorityTasks = allTasks
            .Where(t => !IsDone(t) && (IsHighPriority(t.Priority) || IsOverdue(t, now) || (t.DueDate.HasValue && (t.DueDate.Value - now).TotalHours <= 48)))
            .OrderBy(t => t.DueDate ?? DateTimeOffset.MaxValue)
            .Take(3)
            .Select(t => new DashboardTaskResponse(
                t.Id,
                t.Title,
                t.Description,
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
                0,
                t.SprintId
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
    public IActionResult GenerateAiStrategy()
    {
        Response.Headers["Deprecation"] = "true";
        return StatusCode(StatusCodes.Status410Gone, new
        {
            errorCode = AiErrorCodes.InvalidRequest,
            error = "Endpoint nhận metric từ trình duyệt đã ngừng dùng. Hãy gọi POST /api/ai/dashboard/strategic-brief để Qaly dựng snapshot có quyền ở server."
        });
    }

    private static bool TryReadAiStrategy(string content, out AiStrategyPayload? payload)
    {
        payload = null;
        var firstBrace = content.IndexOf('{');
        var lastBrace = content.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace) return false;

        try
        {
            payload = JsonSerializer.Deserialize<AiStrategyPayload>(
                content[firstBrace..(lastBrace + 1)],
                WebJsonSerializerOptions);
            return payload != null
                && !string.IsNullOrWhiteSpace(payload.Summary)
                && payload.RiskAnalysis.Count > 0
                && payload.Recommendations.Count > 0
                && payload.PriorityPlan.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
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
    Guid? OrganizationId,
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
    DateTimeOffset? EndDate,
    bool EnableOnHold = true,
    bool EnableInReview = true,
    bool RequireEvidenceToDone = false,
    bool RestrictTransitionsToAdmin = false,
    ProjectPermissionsDto? Permissions = null);

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
    string? Description,
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
    int AttachmentCount,
    Guid? SprintId);

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
    Guid? ProjectId,
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
    IReadOnlyList<string> PriorityPlan,
    string Provider,
    string Model,
    bool CacheHit);

public sealed record AiStrategyPayload(
    string Summary,
    IReadOnlyList<string> RiskAnalysis,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> PriorityPlan);

