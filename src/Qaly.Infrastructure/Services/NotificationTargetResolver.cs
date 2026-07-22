using Microsoft.EntityFrameworkCore;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed class NotificationTargetResolver : INotificationTargetResolver
{
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
        if (!notification.RelatedEntityId.HasValue || string.IsNullOrWhiteSpace(notification.RelatedEntityType))
        {
            return new NotificationTargetAccess(true);
        }

        var entityId = notification.RelatedEntityId.Value;
        if (string.Equals(notification.RelatedEntityType, nameof(TaskItem), StringComparison.OrdinalIgnoreCase))
        {
            var task = await _db.TaskItems.AsNoTracking()
                .Include(item => item.Project).ThenInclude(project => project.Organization)
                .Include(item => item.Assignees)
                .FirstOrDefaultAsync(item => item.Id == entityId, cancellationToken);
            if (task == null || !await CanAccessProjectAsync(task.Project, userId, cancellationToken))
            {
                return new NotificationTargetAccess(false);
            }

            if (task.IsPrivate && task.ReporterId != userId && task.AssigneeId != userId &&
                !task.Assignees.Any(assignment => assignment.UserId == userId) &&
                !await CanManageProjectAsync(task.Project, userId, cancellationToken))
            {
                return new NotificationTargetAccess(false);
            }

            return new NotificationTargetAccess(true, $"/projects/{task.ProjectId}/tasks/{task.Id}");
        }

        if (string.Equals(notification.RelatedEntityType, nameof(Project), StringComparison.OrdinalIgnoreCase))
        {
            var project = await _db.Projects.AsNoTracking().Include(item => item.Organization)
                .FirstOrDefaultAsync(item => item.Id == entityId, cancellationToken);
            return project != null && await CanAccessProjectAsync(project, userId, cancellationToken)
                ? new NotificationTargetAccess(true, $"/projects/{project.Id}")
                : new NotificationTargetAccess(false);
        }

        if (string.Equals(notification.RelatedEntityType, nameof(GroupMessage), StringComparison.OrdinalIgnoreCase))
        {
            var message = await _db.GroupMessages.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == entityId, cancellationToken);
            if (message == null || message.IsDeleted || !await CanAccessGroupAsync(message.WorkGroupId, userId, cancellationToken))
            {
                return new NotificationTargetAccess(false);
            }
            return new NotificationTargetAccess(true, $"/groups/{message.WorkGroupId}?messageId={message.Id}");
        }

        if (string.Equals(notification.RelatedEntityType, nameof(WorkGroup), StringComparison.OrdinalIgnoreCase))
        {
            return await CanAccessGroupAsync(entityId, userId, cancellationToken)
                ? new NotificationTargetAccess(true, $"/groups/{entityId}")
                : new NotificationTargetAccess(false);
        }

        if (string.Equals(notification.RelatedEntityType, nameof(GroupMeetingSession), StringComparison.OrdinalIgnoreCase))
        {
            var meeting = await _db.GroupMeetingSessions.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == entityId, cancellationToken);
            return meeting != null && await CanAccessGroupAsync(meeting.WorkGroupId, userId, cancellationToken)
                ? new NotificationTargetAccess(true, $"/groups/{meeting.WorkGroupId}?meetingId={meeting.Id}")
                : new NotificationTargetAccess(false);
        }

        return new NotificationTargetAccess(true);
    }

    private async Task<bool> CanAccessProjectAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (await IsAdminAsync(userId, ct) || project.OwnerId == userId || project.Organization?.OwnerId == userId) return true;
        if (await _db.ProjectMembers.AnyAsync(member => member.ProjectId == project.Id && member.UserId == userId, ct)) return true;
        return project.OrganizationId.HasValue && await _db.OrganizationMembers.AnyAsync(
            member => member.OrganizationId == project.OrganizationId.Value && member.UserId == userId,
            ct);
    }

    private async Task<bool> CanManageProjectAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (await IsAdminAsync(userId, ct) || project.OwnerId == userId || project.Organization?.OwnerId == userId) return true;
        var projectRole = await _db.ProjectMembers
            .Where(member => member.ProjectId == project.Id && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        if (ProjectRoleRules.CanManageProject(projectRole)) return true;
        if (!project.OrganizationId.HasValue) return false;
        var organizationRole = await _db.OrganizationMembers
            .Where(member => member.OrganizationId == project.OrganizationId.Value && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        return ProjectRoleRules.CanManageProject(organizationRole);
    }

    private async Task<bool> CanAccessGroupAsync(Guid groupId, Guid userId, CancellationToken ct)
        => await IsAdminAsync(userId, ct) ||
           await _db.WorkGroups.AnyAsync(group => group.Id == groupId && group.OwnerId == userId, ct) ||
           await _db.WorkGroupMembers.AnyAsync(member => member.WorkGroupId == groupId && member.UserId == userId, ct);

    private Task<bool> IsAdminAsync(Guid userId, CancellationToken ct)
        => _db.Users.AnyAsync(user => user.Id == userId && user.Role == ProjectRoleRules.SystemAdmin, ct);
}
