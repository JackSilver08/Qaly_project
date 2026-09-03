using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed class NotificationTargetResolver : INotificationTargetResolver
{
    private static readonly NotificationTargetAccess Hidden = new(false);
    private readonly QalyDbContext _db;

    public NotificationTargetResolver(QalyDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationTargetAccess> ResolveAsync(
        Notification notification,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return (await ResolveManyAsync([notification], userId, cancellationToken))[0];
    }

    public async Task<IReadOnlyList<NotificationTargetAccess>> ResolveManyAsync(
        IReadOnlyList<Notification> notifications,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (notifications.Count == 0) return [];

        var results = Enumerable.Repeat(Hidden, notifications.Count).ToArray();
        for (var index = 0; index < notifications.Count; index++)
        {
            if (!notifications[index].RelatedEntityId.HasValue ||
                string.IsNullOrWhiteSpace(notifications[index].RelatedEntityType))
            {
                results[index] = new NotificationTargetAccess(true);
            }
        }

        var isSystemAdmin = await IsAdminAsync(userId, cancellationToken);
        var taskIds = TargetIds(notifications, nameof(TaskItem));
        var tasks = taskIds.Count == 0
            ? []
            : await _db.TaskItems.AsNoTracking()
                .Include(item => item.Assignees)
                .Where(item => taskIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var projectIds = TargetIds(notifications, nameof(Project));
        projectIds.UnionWith(tasks.Select(task => task.ProjectId));
        var projects = projectIds.Count == 0
            ? []
            : await _db.Projects.AsNoTracking()
                .Include(item => item.Members)
                .Include(item => item.Organization)
                    .ThenInclude(item => item!.Members)
                .Where(item => projectIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var messageIds = TargetIds(notifications, nameof(GroupMessage));
        var messages = messageIds.Count == 0
            ? []
            : await _db.GroupMessages.IgnoreQueryFilters().AsNoTracking()
                .Where(item => messageIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var meetingIds = TargetIds(notifications, nameof(GroupMeetingSession));
        var meetings = meetingIds.Count == 0
            ? []
            : await _db.GroupMeetingSessions.AsNoTracking()
                .Where(item => meetingIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var groupIds = TargetIds(notifications, nameof(WorkGroup));
        groupIds.UnionWith(messages.Select(message => message.WorkGroupId));
        groupIds.UnionWith(meetings.Select(meeting => meeting.WorkGroupId));
        var groups = groupIds.Count == 0
            ? []
            : await _db.WorkGroups.AsNoTracking()
                .Include(item => item.Members)
                .Include(item => item.Organization)
                    .ThenInclude(item => item!.Members)
                .Where(item => groupIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var customManagerRoles = await LoadCustomManagerRolesAsync(projects, userId, cancellationToken);
        var taskById = tasks.ToDictionary(item => item.Id);
        var projectById = projects.ToDictionary(item => item.Id);
        var messageById = messages.ToDictionary(item => item.Id);
        var meetingById = meetings.ToDictionary(item => item.Id);
        var groupById = groups.ToDictionary(item => item.Id);

        for (var index = 0; index < notifications.Count; index++)
        {
            var notification = notifications[index];
            if (!notification.RelatedEntityId.HasValue || string.IsNullOrWhiteSpace(notification.RelatedEntityType))
            {
                continue;
            }

            var entityId = notification.RelatedEntityId.Value;
            if (IsType(notification, nameof(TaskItem)) &&
                taskById.TryGetValue(entityId, out var task) &&
                projectById.TryGetValue(task.ProjectId, out var taskProject) &&
                CanAccessTask(task, taskProject, userId, isSystemAdmin, customManagerRoles))
            {
                results[index] = new NotificationTargetAccess(true, $"/projects/{task.ProjectId}/tasks/{task.Id}");
            }
            else if (IsType(notification, nameof(Project)) &&
                projectById.TryGetValue(entityId, out var project) &&
                CanAccessProject(project, userId, isSystemAdmin))
            {
                results[index] = new NotificationTargetAccess(true, $"/projects/{project.Id}");
            }
            else if (IsType(notification, nameof(GroupMessage)) &&
                messageById.TryGetValue(entityId, out var message) && !message.IsDeleted &&
                groupById.TryGetValue(message.WorkGroupId, out var messageGroup) &&
                CanAccessGroup(messageGroup, userId, isSystemAdmin))
            {
                results[index] = new NotificationTargetAccess(true, $"/groups/{message.WorkGroupId}?messageId={message.Id}");
            }
            else if (IsType(notification, nameof(WorkGroup)) &&
                groupById.TryGetValue(entityId, out var group) &&
                CanAccessGroup(group, userId, isSystemAdmin))
            {
                results[index] = new NotificationTargetAccess(true, $"/groups/{group.Id}");
            }
            else if (IsType(notification, nameof(GroupMeetingSession)) &&
                meetingById.TryGetValue(entityId, out var meeting) &&
                groupById.TryGetValue(meeting.WorkGroupId, out var meetingGroup) &&
                CanAccessGroup(meetingGroup, userId, isSystemAdmin))
            {
                results[index] = new NotificationTargetAccess(true, $"/groups/{meeting.WorkGroupId}/meeting?meetingId={meeting.Id}");
            }
        }

        // A notification for an unsupported entity cannot provide a verified canonical target.
        // Hide it rather than showing a dead or potentially cross-tenant link.
        return results;
    }

    private static HashSet<Guid> TargetIds(IReadOnlyList<Notification> notifications, string entityType)
        => notifications
            .Where(notification => notification.RelatedEntityId.HasValue &&
                string.Equals(notification.RelatedEntityType, entityType, StringComparison.OrdinalIgnoreCase))
            .Select(notification => notification.RelatedEntityId!.Value)
            .ToHashSet();

    private static bool IsType(Notification notification, string entityType)
        => string.Equals(notification.RelatedEntityType, entityType, StringComparison.OrdinalIgnoreCase);

    private static bool CanAccessProject(Project project, Guid userId, bool isSystemAdmin)
    {
        if (isSystemAdmin) return true;
        if (project.OrganizationId.HasValue)
        {
            var organization = project.Organization;
            if (organization == null || !organization.IsActive) return false;
            if (organization.OwnerId == userId)
            {
                return true;
            }

            var organizationRole = organization.Members
                .Where(member => member.UserId == userId)
                .Select(member => member.Role)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(organizationRole))
            {
                return false;
            }

            if (OrganizationRoleRules.CanManageOrganization(organizationRole)) return true;
        }

        return project.OwnerId == userId || project.Members.Any(member => member.UserId == userId);
    }

    private static bool CanAccessTask(
        TaskItem task,
        Project project,
        Guid userId,
        bool isSystemAdmin,
        HashSet<(Guid OrganizationId, string RoleKey)> customManagerRoles)
    {
        if (!CanAccessProject(project, userId, isSystemAdmin)) return false;
        if (!task.IsPrivate || isSystemAdmin || task.ReporterId == userId || task.AssigneeId == userId ||
            task.Assignees.Any(assignment => assignment.UserId == userId) || project.OwnerId == userId)
        {
            return true;
        }

        var membershipRole = project.Members
            .Where(member => member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefault();
        return ProjectRoleRules.IsProjectManager(membershipRole) ||
            (project.OrganizationId.HasValue && !string.IsNullOrWhiteSpace(membershipRole) &&
             customManagerRoles.Contains((project.OrganizationId.Value, membershipRole.ToUpperInvariant())));
    }

    private static bool CanAccessGroup(WorkGroup group, Guid userId, bool isSystemAdmin)
        => isSystemAdmin ||
           ((group.OwnerId == userId || group.Members.Any(member => member.UserId == userId)) &&
            (group.OrganizationId == null ||
             (group.Organization != null && group.Organization.IsActive &&
              (group.Organization.OwnerId == userId ||
               group.Organization.Members.Any(member => member.UserId == userId)))));

    private async Task<HashSet<(Guid OrganizationId, string RoleKey)>> LoadCustomManagerRolesAsync(
        IReadOnlyCollection<Project> projects,
        Guid userId,
        CancellationToken ct)
    {
        var organizationIds = projects
            .Where(project => project.OrganizationId.HasValue)
            .Select(project => project.OrganizationId!.Value)
            .Distinct()
            .ToArray();
        if (organizationIds.Length == 0) return [];

        var definitions = await _db.ProjectRoleDefinitions.AsNoTracking()
            .Where(definition => organizationIds.Contains(definition.OrganizationId) && definition.IsActive &&
                (definition.BaseRole == ProjectRoleRules.Owner ||
                 definition.BaseRole == ProjectRoleRules.Manager ||
                 definition.BaseRole == ProjectRoleRules.ScrumMaster))
            .Select(definition => new { definition.OrganizationId, definition.Key })
            .ToListAsync(ct);

        var memberRoles = projects
            .SelectMany(project => project.Members
                .Where(member => member.UserId == userId && project.OrganizationId.HasValue)
                .Select(member => (project.OrganizationId!.Value, member.Role.ToUpperInvariant())))
            .ToHashSet();

        return definitions
            .Select(definition => (definition.OrganizationId, definition.Key.ToUpperInvariant()))
            .Where(memberRoles.Contains)
            .ToHashSet();
    }

    private Task<bool> IsAdminAsync(Guid userId, CancellationToken ct)
        => _db.Users.AnyAsync(user =>
            user.Id == userId && user.IsActive && user.Role == ProjectRoleRules.SystemAdmin,
            ct);
}
