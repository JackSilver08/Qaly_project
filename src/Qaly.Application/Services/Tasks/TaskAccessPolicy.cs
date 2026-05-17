using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services.Tasks;

public sealed class TaskAccessPolicy : ITaskAccessPolicy
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IRepository<ProjectMember> _memberRepo;

    public TaskAccessPolicy(
        ICurrentUserService currentUserService,
        IRepository<ProjectMember> memberRepo)
    {
        _currentUserService = currentUserService;
        _memberRepo = memberRepo;
    }

    public Guid? CurrentUserId => _currentUserService.UserId;

    public bool IsAdmin
        => string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public IQueryable<TaskItem> ApplyVisibilityFilter(IQueryable<TaskItem> query)
    {
        if (IsAdmin)
        {
            return query;
        }

        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return query.Where(task => false);
        }

        return query.Where(task =>
            !task.IsPrivate ||
            task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Assignees.Any(assignment => assignment.UserId == currentUserId) ||
            task.Project.OwnerId == currentUserId);
    }

    public async Task<bool> CanAccessTaskAsync(TaskItem task, CancellationToken ct)
    {
        if (IsAdmin)
        {
            return true;
        }

        if (!await CanAccessProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
        {
            return false;
        }

        if (!task.IsPrivate)
        {
            return true;
        }

        var currentUserId = CurrentUserId;
        return task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Assignees.Any(assignment => assignment.UserId == currentUserId) ||
            task.Project.OwnerId == currentUserId;
    }

    public async Task<bool> CanManageTaskAsync(TaskItem task, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin ||
            task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Assignees.Any(assignment => assignment.UserId == currentUserId) ||
            task.Project.OwnerId == currentUserId)
        {
            return true;
        }

        return await _memberRepo.GetQueryable()
            .AnyAsync(member =>
                member.ProjectId == task.ProjectId &&
                member.UserId == currentUserId &&
                (member.Role == "Admin" || member.Role == "Owner"),
                ct);
    }

    public async Task<bool> CanAccessProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin || ownerId == currentUserId)
        {
            return true;
        }

        return await _memberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == currentUserId, ct);
    }

    public async Task<bool> CanViewProjectTimelineAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => member.CanViewProjectTimeline || IsElevatedProjectRole(member.Role),
            ct);

    public async Task<bool> CanViewTaskRiskAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => member.CanViewTaskRisk || IsElevatedProjectRole(member.Role),
            ct);

    public async Task<bool> CanViewUnseenTaskSignalAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => member.CanViewUnseenTaskSignal || IsElevatedProjectRole(member.Role),
            ct);

    public async Task<bool> CanNudgeAssigneeAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => member.CanNudgeAssignee || IsElevatedProjectRole(member.Role),
            ct);

    private async Task<bool> HasProjectPermissionAsync(
        Guid projectId,
        Guid ownerId,
        Func<ProjectMember, bool> predicate,
        CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin || ownerId == currentUserId)
        {
            return true;
        }

        var member = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.UserId == currentUserId, ct);

        return member != null && predicate(member);
    }

    private static bool IsElevatedProjectRole(string? role)
        => string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "ProjectOwner", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "PM", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "ProjectManager", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "ScrumMaster", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
}
