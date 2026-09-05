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
    private readonly IProjectRoleCatalog _roleCatalog;

    public TaskAccessPolicy(
        ICurrentUserService currentUserService,
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IProjectRoleCatalog roleCatalog)
    {
        _currentUserService = currentUserService;
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _roleCatalog = roleCatalog;
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

        var customManagerRoles = _roleCatalog.GetDefinitionsQuery()
            .Where(definition =>
                definition.BaseRole == ProjectRoleRules.Manager ||
                definition.BaseRole == ProjectRoleRules.ScrumMaster ||
                definition.BaseRole == ProjectRoleRules.Owner);

        return query.Where(task =>
            (
                task.Project.OwnerId == currentUserId ||
                task.Project.Members.Any(member => member.UserId == currentUserId) ||
                (task.Project.Organization != null &&
                 (task.Project.Organization.OwnerId == currentUserId ||
                  task.Project.Organization.Members.Any(member =>
                      member.UserId == currentUserId &&
                      (member.Role == OrganizationRoleRules.OrganizationAdmin ||
                       member.Role == "Admin" ||
                       member.Role == "Manager"))))
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
                task.Project.OwnerId == currentUserId ||
                task.Project.Members.Any(member =>
                    member.UserId == currentUserId &&
                    (member.Role == ProjectRoleRules.Manager ||
                     member.Role == ProjectRoleRules.ScrumMaster ||
                     member.Role == ProjectRoleRules.Owner ||
                     member.Role == "PM" ||
                     member.Role == "ProjectOwner" ||
                     member.Role == "ProjectManager" ||
                     member.Role == "Project Manager" ||
                     (task.Project.OrganizationId != null &&
                      customManagerRoles.Any(definition =>
                          definition.OrganizationId == task.Project.OrganizationId &&
                          definition.Key == member.Role &&
                          (definition.BaseRole == ProjectRoleRules.Manager ||
                           definition.BaseRole == ProjectRoleRules.ScrumMaster ||
                           definition.BaseRole == ProjectRoleRules.Owner))))
            )));
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
            task.Project.OwnerId == currentUserId ||
            await HasProjectManagementMembershipAsync(task.ProjectId, ct);
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

        // Reporter/assignee identity is not a substitute for current Project write
        // permission. A removed or read-only member may remain in historical rows,
        // but must lose mutation paths immediately.
        if (!await CanContributeToTaskAsync(task, ct))
        {
            return false;
        }

        if (IsAdmin || task.Project.OwnerId == currentUserId ||
            await CanManageProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
        {
            return true;
        }

        return task.ReporterId == currentUserId ||
            task.AssigneeId == currentUserId ||
            task.Assignees.Any(assignment => assignment.UserId == currentUserId);
    }

    public async Task<bool> CanReviewTaskAsync(TaskItem task, CancellationToken ct)
    {
        var actorId = CurrentUserId;
        if (actorId == null || task.Project == null || !await CanContributeToTaskAsync(task, ct))
            return false;

        if (await CanManageProjectAsync(task.ProjectId, task.Project.OwnerId, ct))
            return true;

        // An assignee cannot approve their own work by also being its reviewer.
        if (task.AssigneeId == actorId || task.Assignees.Any(assignment => assignment.UserId == actorId))
            return false;

        if (task.ReviewerId == actorId) return true;

        var role = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == task.ProjectId && member.UserId == actorId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        var baseRole = await ResolveBaseRoleAsync(task.ProjectId, role, ct);
        return baseRole != null && ProjectPermissionRules.Resolve(
            baseRole, isOwner: false, isSystemAdmin: false).CanReviewEvidence;
    }

    public async Task<bool> CanContributeToTaskAsync(TaskItem task, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null || task.Project == null ||
            !await CanAccessTaskAsync(task, ct))
        {
            return false;
        }

        return await CanContributeToProjectAsync(task.ProjectId, task.Project.OwnerId, ct);
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

        var organizationRole = projectOrganizationInfo.OrganizationId.HasValue
            ? await _organizationMemberRepo.GetQueryable()
                .Where(member =>
                    member.OrganizationId == projectOrganizationInfo.OrganizationId.Value &&
                    member.UserId == currentUserId)
                .Select(member => member.Role)
                .FirstOrDefaultAsync(ct)
            : null;

        if (projectOrganizationInfo.OrganizationId.HasValue &&
            projectOrganizationInfo.OrganizationOwnerId != currentUserId &&
            string.IsNullOrWhiteSpace(organizationRole))
        {
            return false;
        }

        if (projectOrganizationInfo.OrganizationId.HasValue &&
            (projectOrganizationInfo.OrganizationOwnerId == currentUserId ||
             OrganizationRoleRules.CanManageOrganization(organizationRole)))
        {
            return true;
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

        if (IsAdmin || ownerId == currentUserId)
        {
            return true;
        }

        if (await HasOrganizationManagementAuthorityAsync(projectId, currentUserId.Value, ct))
        {
            return true;
        }

        var memberRole = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        var baseRole = await ResolveBaseRoleAsync(projectId, memberRole, ct);
        if (ProjectRoleRules.CanManageProject(baseRole))
        {
            return true;
        }

        return false;
    }

    public async Task<bool> CanViewProjectTimelineAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => member.CanViewProjectTimeline,
            ct);

    public async Task<bool> CanViewTaskRiskAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => member.CanViewTaskRisk,
            ct);

    public async Task<bool> CanViewUnseenTaskSignalAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => member.CanViewUnseenTaskSignal,
            ct);

    public async Task<bool> CanNudgeAssigneeAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            member => member.CanNudgeAssignee,
            ct);

    public async Task<bool> CanManageWebhooksAsync(Guid projectId, Guid ownerId, CancellationToken ct)
        => await HasProjectPermissionAsync(
            projectId,
            ownerId,
            _ => false,
            ct);

    public async Task<bool> CanViewProjectWorkloadAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null || !await CanAccessProjectAsync(projectId, ownerId, ct))
        {
            return false;
        }

        if (IsAdmin || ownerId == currentUserId ||
            await HasOrganizationManagementAuthorityAsync(projectId, currentUserId.Value, ct))
        {
            return true;
        }

        var storedRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        var baseRole = await ResolveBaseRoleAsync(projectId, storedRole, ct);
        return baseRole != null && !ProjectRoleRules.IsCustomer(baseRole);
    }

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

        if (IsAdmin || ownerId == currentUserId ||
            await HasOrganizationManagementAuthorityAsync(projectId, currentUserId.Value, ct))
        {
            return true;
        }

        var projectRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        var baseRole = await ResolveBaseRoleAsync(projectId, projectRole, ct);
        return baseRole != null && !ProjectRoleRules.IsCustomer(baseRole);
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

        if (IsAdmin || ownerId == currentUserId ||
            await HasOrganizationManagementAuthorityAsync(projectId, currentUserId.Value, ct))
        {
            return true;
        }

        var memberRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        var baseRole = await ResolveBaseRoleAsync(projectId, memberRole, ct);
        return ProjectRoleRules.CanWrite(baseRole);
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

        if (IsAdmin || ownerId == currentUserId ||
            await HasOrganizationManagementAuthorityAsync(projectId, currentUserId.Value, ct))
        {
            return true;
        }

        var member = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.UserId == currentUserId, ct);

        if (member != null && (predicate(member) ||
            ProjectRoleRules.CanManageProject(await ResolveBaseRoleAsync(projectId, member.Role, ct))))
        {
            return true;
        }

        return false;
    }

    public async Task<bool> CanContributeToProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null || !await CanAccessProjectAsync(projectId, ownerId, ct))
        {
            return false;
        }

        if (IsAdmin || ownerId == currentUserId ||
            await HasOrganizationManagementAuthorityAsync(projectId, currentUserId.Value, ct))
        {
            return true;
        }

        var storedRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        var baseRole = await ResolveBaseRoleAsync(projectId, storedRole, ct);
        return ProjectRoleRules.CanWrite(baseRole);
    }

    public async Task<bool> CanCreateTaskAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null || !await CanAccessProjectAsync(projectId, ownerId, ct))
        {
            return false;
        }

        if (IsAdmin || ownerId == currentUserId ||
            await HasOrganizationManagementAuthorityAsync(projectId, currentUserId.Value, ct))
        {
            return true;
        }

        var storedRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        var baseRole = await ResolveBaseRoleAsync(projectId, storedRole, ct);
        return ProjectPermissionRules.Resolve(
            baseRole,
            isOwner: false,
            isSystemAdmin: false).CanCreateTask;
    }

    private async Task<string?> ResolveBaseRoleAsync(Guid projectId, string? storedRole, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storedRole)) return null;
        var organizationId = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.OrganizationId)
            .FirstOrDefaultAsync(ct);
        return (await _roleCatalog.ResolveAsync(storedRole, organizationId, ct))?.BaseRole;
    }

    private async Task<bool> HasProjectManagementMembershipAsync(Guid projectId, CancellationToken ct)
    {
        var currentUserId = CurrentUserId;
        if (currentUserId == null)
        {
            return false;
        }

        var storedRole = await _memberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        var baseRole = await ResolveBaseRoleAsync(projectId, storedRole, ct);
        return ProjectRoleRules.CanManageProject(baseRole);
    }

    private async Task<bool> HasOrganizationManagementAuthorityAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct)
    {
        var authority = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .Where(project => project.Id == projectId && project.Organization != null && project.Organization.IsActive)
            .Select(project => new
            {
                IsOwner = project.Organization!.OwnerId == userId,
                Role = project.Organization.Members
                    .Where(member => member.UserId == userId)
                    .Select(member => member.Role)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        return authority != null &&
            (authority.IsOwner || OrganizationRoleRules.CanManageOrganization(authority.Role));
    }
}
