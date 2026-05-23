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
                task.Project.Members.Any(member => member.UserId == currentUserId) ||
                (task.Project.OrganizationId != null &&
                 (task.Project.Organization!.OwnerId == currentUserId ||
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

        return await CanManageOrganizationForProjectAsync(task.ProjectId, currentUserId.Value, ct);
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

        var isProjectMember = await _memberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == currentUserId, ct);
        if (isProjectMember)
        {
            return true;
        }

        var projectOrganizationInfo = await _projectRepo.GetQueryable()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                project.OrganizationId,
                OrganizationOwnerId = project.Organization != null ? (Guid?)project.Organization.OwnerId : null
            })
            .FirstOrDefaultAsync(ct);

        if (projectOrganizationInfo?.OrganizationId == null || projectOrganizationInfo.OrganizationOwnerId == null)
        {
            return false;
        }

        if (projectOrganizationInfo.OrganizationOwnerId == currentUserId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member =>
                member.OrganizationId == projectOrganizationInfo.OrganizationId.Value &&
                member.UserId == currentUserId,
                ct);
    }

    public async Task<bool> CanManageProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
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

        var memberRole = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (memberRole != null && ProjectRoleRules.CanManageProject(memberRole))
        {
            return true;
        }

        return await CanManageOrganizationForProjectAsync(projectId, currentUserId.Value, ct);
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

        if (IsAdmin || ownerId == currentUserId)
        {
            return true;
        }

        var projectRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (projectRole != null)
        {
            return !ProjectRoleRules.IsCustomer(projectRole);
        }

        var projectOrganizationInfo = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                project.OrganizationId,
                OrganizationOwnerId = project.Organization != null ? (Guid?)project.Organization.OwnerId : null
            })
            .FirstOrDefaultAsync(ct);

        if (projectOrganizationInfo?.OrganizationId == null || projectOrganizationInfo.OrganizationOwnerId == null)
        {
            return false;
        }

        if (projectOrganizationInfo.OrganizationOwnerId == currentUserId)
        {
            return true;
        }

        var organizationRole = await _organizationMemberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member =>
                member.OrganizationId == projectOrganizationInfo.OrganizationId.Value &&
                member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return !ProjectRoleRules.IsCustomer(organizationRole);
    }

    public async Task<bool> CanWriteWikiAsync(Guid projectId, Guid ownerId, CancellationToken ct)
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

        var memberRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        if (memberRole != null)
        {
            return !ProjectRoleRules.IsViewer(memberRole) && !ProjectRoleRules.IsCustomer(memberRole);
        }

        return await CanManageOrganizationForProjectAsync(projectId, currentUserId.Value, ct);
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

        return await CanManageOrganizationForProjectAsync(projectId, currentUserId.Value, ct);
    }

    private async Task<bool> CanManageOrganizationForProjectAsync(Guid projectId, Guid currentUserId, CancellationToken ct)
    {
        var projectOrganizationInfo = await _projectRepo.GetQueryable()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                project.OrganizationId,
                OrganizationOwnerId = project.Organization != null ? (Guid?)project.Organization.OwnerId : null
            })
            .FirstOrDefaultAsync(ct);

        if (projectOrganizationInfo?.OrganizationId == null || projectOrganizationInfo.OrganizationOwnerId == null)
        {
            return false;
        }

        if (projectOrganizationInfo.OrganizationOwnerId == currentUserId)
        {
            return true;
        }

        var organizationRole = await _organizationMemberRepo.GetQueryable()
            .Where(member =>
                member.OrganizationId == projectOrganizationInfo.OrganizationId.Value &&
                member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return ProjectRoleRules.CanManageProject(organizationRole);
    }

    private static bool IsElevatedProjectRole(string? role)
        => string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "ProjectOwner", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "PM", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "ProjectManager", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "ScrumMaster", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
}
