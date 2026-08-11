using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services.Tasks;

public sealed class TaskAccessPolicy : ITaskAccessPolicy
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;

    public TaskAccessPolicy(
        ICurrentUserService currentUserService,
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<OrganizationMember> organizationMemberRepo)
    {
        _currentUserService = currentUserService;
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
        _organizationMemberRepo = organizationMemberRepo;
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
            (
                task.Project.OwnerId == currentUserId ||
                task.Project.Members.Any(member => member.UserId == currentUserId)
            )
            &&
            (
                task.Project.OrganizationId == null ||
                (task.Project.Organization != null &&
                 task.Project.Organization.IsActive &&
                 (task.Project.Organization.OwnerId == currentUserId ||
                  task.Project.Organization.Members.Any(member => member.UserId == currentUserId)))
            )
            &&
            (
                !task.IsPrivate ||
                task.ReporterId == currentUserId ||
                task.AssigneeId == currentUserId ||
                task.Assignees.Any(assignment => assignment.UserId == currentUserId) ||
                task.Project.OwnerId == currentUserId
            ));
    }

    public async Task<bool> CanAccessTaskAsync(TaskItem task, CancellationToken ct)
    {
        if (IsAdmin)
        {
            return true;
        }

        if (task.Project == null)
        {
            return false;
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

        if (task.Project == null)
        {
            return false;
        }

        if (IsAdmin)
        {
            return true;
        }

        // Reporter/assignee identity is not a substitute for current project access.
        // A removed member may remain in historical assignment rows, but must lose
        // every mutation path as soon as project/organization access is revoked.
        if (!await CanAccessProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
        {
            return false;
        }

        if (
            task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Assignees.Any(assignment => assignment.UserId == currentUserId) ||
            task.Project.OwnerId == currentUserId)
        {
            return true;
        }

        var canManageProjectByRole = await _memberRepo.GetQueryable()
            .AnyAsync(member =>
                member.ProjectId == task.ProjectId &&
                member.UserId == currentUserId &&
                ProjectRoleRules.CanManageProject(member.Role),
                ct);
        if (canManageProjectByRole)
        {
            return true;
        }

        return false;
    }

    public async Task<bool> CanAccessProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin)
        {
            return true;
        }

        var projectOrganizationInfo = await _projectRepo.GetQueryable()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                project.OrganizationId,
                OrganizationIsActive = project.Organization == null || project.Organization.IsActive,
                OrganizationOwnerId = project.Organization != null ? (Guid?)project.Organization.OwnerId : null
            })
            .FirstOrDefaultAsync(ct);

        if (projectOrganizationInfo == null || !projectOrganizationInfo.OrganizationIsActive)
        {
            return false;
        }

        if (projectOrganizationInfo.OrganizationId.HasValue &&
            projectOrganizationInfo.OrganizationOwnerId != currentUserId &&
            !await _organizationMemberRepo.GetQueryable().AnyAsync(member =>
                member.OrganizationId == projectOrganizationInfo.OrganizationId.Value &&
                member.UserId == currentUserId,
                ct))
        {
            return false;
        }

        if (ownerId == currentUserId)
        {
            return true;
        }

        return await _memberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == currentUserId, ct);
    }

    public async Task<bool> CanManageProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (!await CanAccessProjectAsync(projectId, ownerId, ct))
        {
            return false;
        }

        if (!await CanAccessProjectAsync(projectId, ownerId, ct))
        {
            return false;
        }

        if (IsAdmin || ownerId == currentUserId)
        {
            return true;
        }

        var memberRole = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (memberRole != null && ProjectRoleRules.CanManageProject(memberRole))
        {
            return true;
        }

        return false;
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

    public async Task<bool> CanManageWebhooksAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => IsElevatedProjectRole(member.Role),
            ct);

    public async Task<bool> CanReadWikiAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await CanAccessProjectAsync(projectId, ownerId, ct);

    public async Task<bool> CanReadInternalWikiAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (!await CanAccessProjectAsync(projectId, ownerId, ct))
        {
            return false;
        }

        if (IsAdmin || ownerId == currentUserId)
        {
            return true;
        }

        var projectRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return projectRole != null && !ProjectRoleRules.IsCustomer(projectRole);
    }

    public async Task<bool> CanWriteWikiAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (!await CanAccessProjectAsync(projectId, ownerId, ct))
        {
            return false;
        }

        if (IsAdmin || ownerId == currentUserId)
        {
            return true;
        }

        var memberRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (memberRole != null)
        {
            return !ProjectRoleRules.IsViewer(memberRole) && !ProjectRoleRules.IsCustomer(memberRole);
        }

        return false;
    }

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

        if (!await CanAccessProjectAsync(projectId, ownerId, ct))
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

        if (member != null && predicate(member))
        {
            return true;
        }

        return false;
    }

    private static bool IsElevatedProjectRole(string? role)
        => ProjectRoleRules.CanManageProject(role);
}
