using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public interface IProjectRoleDefinitionService
{
    Task<Result<IReadOnlyList<ProjectRoleDefinitionDto>>> GetByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AssignableProjectRoleDto>>> GetAssignableForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<ProjectRoleDefinitionDto>> CreateAsync(Guid organizationId, CreateProjectRoleDefinitionDto dto, CancellationToken ct = default);
    Task<Result<ProjectRoleDefinitionDto>> UpdateAsync(Guid definitionId, UpdateProjectRoleDefinitionDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid definitionId, CancellationToken ct = default);
}

/// <summary>
/// Lets an organization owner define roles that match how their team actually works, without
/// inventing new permissions: every custom role inherits a built-in role's permission level.
/// </summary>
public class ProjectRoleDefinitionService : IProjectRoleDefinitionService
{
    private readonly IRepository<ProjectRoleDefinition> _definitionRepo;
    private readonly IRepository<Organization> _organizationRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _projectMemberRepo;
    private readonly IProjectRoleCatalog _roleCatalog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public ProjectRoleDefinitionService(
        IRepository<ProjectRoleDefinition> definitionRepo,
        IRepository<Organization> organizationRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> projectMemberRepo,
        IProjectRoleCatalog roleCatalog,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _definitionRepo = definitionRepo;
        _organizationRepo = organizationRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _projectRepo = projectRepo;
        _projectMemberRepo = projectMemberRepo;
        _roleCatalog = roleCatalog;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<IReadOnlyList<ProjectRoleDefinitionDto>>> GetByOrganizationAsync(
        Guid organizationId,
        CancellationToken ct = default)
    {
        if (!await CanReadOrganizationAsync(organizationId, ct))
        {
            return Result.Forbidden<IReadOnlyList<ProjectRoleDefinitionDto>>();
        }

        var definitions = await _definitionRepo.GetQueryable()
            .AsNoTracking()
            .Where(definition => definition.OrganizationId == organizationId)
            .OrderBy(definition => definition.DisplayName)
            .ToListAsync(ct);

        var usageCounts = await CountMembersPerRoleAsync(organizationId, ct);

        return Result.Success<IReadOnlyList<ProjectRoleDefinitionDto>>(
            definitions.Select(definition => ToDto(definition, usageCounts)).ToList());
    }

    public async Task<Result<IReadOnlyList<AssignableProjectRoleDto>>> GetAssignableForProjectAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var project = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId, ct);

        if (project == null)
        {
            return Result.NotFound<IReadOnlyList<AssignableProjectRoleDto>>();
        }

        if (!await CanAccessProjectAsync(project, ct))
        {
            return Result.Forbidden<IReadOnlyList<AssignableProjectRoleDto>>();
        }

        var roles = await _roleCatalog.GetAssignableRolesAsync(project.OrganizationId, ct);

        return Result.Success<IReadOnlyList<AssignableProjectRoleDto>>(
            roles.Select(role =>
            {
                var permissions = ProjectPermissionRules.Resolve(role.BaseRole, isOwner: false, isSystemAdmin: false);
                var aiTier = AiCapabilityRules.ResolveTier(role.BaseRole);
                return new AssignableProjectRoleDto(
                    role.Key,
                    role.DisplayName,
                    role.BaseRole,
                    role.IsCustom,
                    SummarizePermissions(permissions),
                    aiTier.ToString(),
                    AiCapabilityRules.DescribeVietnamese(aiTier),
                    role.SkillTags);
            }).ToList());
    }

    public async Task<Result<ProjectRoleDefinitionDto>> CreateAsync(
        Guid organizationId,
        CreateProjectRoleDefinitionDto dto,
        CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<ProjectRoleDefinitionDto>();
        }

        if (!await CanManageOrganizationAsync(organizationId, ct))
        {
            return Result.Forbidden<ProjectRoleDefinitionDto>();
        }

        var validation = ValidateInput(dto.DisplayName, dto.BaseRole, out var key, out var baseRole);
        if (validation != null)
        {
            return Result.Failure<ProjectRoleDefinitionDto>(validation, 400);
        }

        if (await _definitionRepo.GetQueryable()
            .AnyAsync(item => item.OrganizationId == organizationId && item.Key == key, ct))
        {
            return Result.Failure<ProjectRoleDefinitionDto>(
                $"A role with key '{key}' already exists in this organization.",
                409);
        }

        var definition = new ProjectRoleDefinition
        {
            OrganizationId = organizationId,
            Key = key,
            DisplayName = dto.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            BaseRole = baseRole,
            SkillTags = NormalizeSkillTags(dto.SkillTags),
            IsActive = true,
            CreatedByUserId = currentUserId.Value
        };

        await _definitionRepo.AddAsync(definition, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(
            "CreateProjectRoleDefinition",
            nameof(ProjectRoleDefinition),
            definition.Id.ToString(),
            new { organizationId, definition.Key, definition.BaseRole },
            ct);

        return Result.Created(ToDto(definition, new Dictionary<string, int>(StringComparer.Ordinal)));
    }

    public async Task<Result<ProjectRoleDefinitionDto>> UpdateAsync(
        Guid definitionId,
        UpdateProjectRoleDefinitionDto dto,
        CancellationToken ct = default)
    {
        var definition = await _definitionRepo.GetByIdAsync(definitionId, ct);
        if (definition == null)
        {
            return Result.NotFound<ProjectRoleDefinitionDto>();
        }

        if (!await CanManageOrganizationAsync(definition.OrganizationId, ct))
        {
            return Result.Forbidden<ProjectRoleDefinitionDto>();
        }

        var validation = ValidateInput(dto.DisplayName, dto.BaseRole, out _, out var baseRole);
        if (validation != null)
        {
            return Result.Failure<ProjectRoleDefinitionDto>(validation, 400);
        }

        // The key stays fixed: project members already store it.
        var previousBaseRole = definition.BaseRole;
        definition.DisplayName = dto.DisplayName.Trim();
        definition.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        definition.BaseRole = baseRole;
        definition.SkillTags = NormalizeSkillTags(dto.SkillTags);
        definition.IsActive = dto.IsActive;
        definition.UpdatedAt = DateTimeOffset.UtcNow;

        await _definitionRepo.UpdateAsync(definition, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(
            "UpdateProjectRoleDefinition",
            nameof(ProjectRoleDefinition),
            definition.Id.ToString(),
            new { definition.Key, previousBaseRole, newBaseRole = baseRole, definition.IsActive },
            ct);

        var usageCounts = await CountMembersPerRoleAsync(definition.OrganizationId, ct);
        return Result.Success(ToDto(definition, usageCounts));
    }

    public async Task<Result> DeleteAsync(Guid definitionId, CancellationToken ct = default)
    {
        var definition = await _definitionRepo.GetByIdAsync(definitionId, ct);
        if (definition == null)
        {
            return Result.Failure("Role definition was not found.", 404);
        }

        if (!await CanManageOrganizationAsync(definition.OrganizationId, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        // Deleting a role in use would leave members with an unresolvable role, which reads as
        // "not a member". Deactivating keeps existing assignments working while hiding the role
        // from the picker.
        var inUse = await _projectMemberRepo.GetQueryable()
            .AnyAsync(member =>
                member.Role == definition.Key &&
                member.Project.OrganizationId == definition.OrganizationId,
                ct);

        if (inUse)
        {
            return Result.Failure(
                "This role is still assigned to project members. Deactivate it instead, or move those members to another role first.",
                409);
        }

        await _definitionRepo.DeleteAsync(definition, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(
            "DeleteProjectRoleDefinition",
            nameof(ProjectRoleDefinition),
            definitionId.ToString(),
            new { definition.OrganizationId, definition.Key },
            ct);

        return Result.Success();
    }

    private static string? ValidateInput(
        string? displayName,
        string? baseRole,
        out string key,
        out string normalizedBaseRole)
    {
        key = string.Empty;
        normalizedBaseRole = ProjectRoleRules.Member;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Role name is required.";
        }

        if (displayName.Trim().Length > 80)
        {
            return "Role name must be 80 characters or fewer.";
        }

        key = ProjectRoleCatalog.NormalizeKey(displayName);
        if (string.IsNullOrEmpty(key))
        {
            return "Role name must contain at least one letter or digit.";
        }

        // A custom role must not shadow a built-in one, otherwise resolution becomes ambiguous.
        if (ProjectRoleRules.TryNormalizeAssignableRole(key, out _)
            || string.Equals(key, ProjectRoleRules.Owner, StringComparison.OrdinalIgnoreCase))
        {
            return $"'{displayName.Trim()}' collides with a built-in role name. Choose a different name.";
        }

        if (!ProjectRoleRules.TryNormalizeAssignableRole(baseRole, out normalizedBaseRole))
        {
            return $"Base role must be one of: {string.Join(", ", ProjectRoleRules.AssignableRoles)}.";
        }

        return null;
    }

    private static string? NormalizeSkillTags(string? skillTags)
    {
        var tags = ProjectRoleCatalog.SplitSkillTags(skillTags);
        return tags.Count == 0 ? null : string.Join(",", tags);
    }

    private async Task<Dictionary<string, int>> CountMembersPerRoleAsync(Guid organizationId, CancellationToken ct)
        => await _projectMemberRepo.GetQueryable()
            .AsNoTracking()
            .Where(member => member.Project.OrganizationId == organizationId)
            .GroupBy(member => member.Role)
            .Select(group => new { Role = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Role, item => item.Count, StringComparer.Ordinal, ct);

    private static ProjectRoleDefinitionDto ToDto(
        ProjectRoleDefinition definition,
        Dictionary<string, int> usageCounts)
        => new(
            definition.Id,
            definition.OrganizationId,
            definition.Key,
            definition.DisplayName,
            definition.Description,
            definition.BaseRole,
            ProjectPermissionRules.DescribeRoleVietnamese(definition.BaseRole),
            ProjectRoleCatalog.SplitSkillTags(definition.SkillTags),
            definition.IsActive,
            usageCounts.TryGetValue(definition.Key, out var count) ? count : 0,
            definition.CreatedAt);

    private static string SummarizePermissions(DTOs.Project.ProjectPermissionsDto permissions)
    {
        if (permissions.CanManageProject) return "Quản lý toàn bộ dự án và thành viên.";
        if (permissions.CanCreateTask) return "Tạo và xử lý task, xem phân tích nhóm.";
        if (permissions.CanUpdateOwnTasks) return "Làm task được giao, bình luận, chấm công.";
        return "Chỉ đọc.";
    }

    private async Task<bool> CanReadOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (ProjectRoleRules.IsSystemAdmin(_currentUserService.Role))
        {
            return true;
        }

        if (await _organizationRepo.GetQueryable()
            .AnyAsync(organization => organization.Id == organizationId && organization.OwnerId == currentUserId, ct))
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == organizationId && member.UserId == currentUserId, ct);
    }

    private async Task<bool> CanManageOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (ProjectRoleRules.IsSystemAdmin(_currentUserService.Role))
        {
            return true;
        }

        if (await _organizationRepo.GetQueryable()
            .AnyAsync(organization => organization.Id == organizationId && organization.OwnerId == currentUserId, ct))
        {
            return true;
        }

        var role = await _organizationMemberRepo.GetQueryable()
            .Where(member => member.OrganizationId == organizationId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return OrganizationRoleRules.CanManageOrganization(role);
    }

    private async Task<bool> CanAccessProjectAsync(Project project, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (ProjectRoleRules.IsSystemAdmin(_currentUserService.Role) || project.OwnerId == currentUserId)
        {
            return true;
        }

        return await _projectMemberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == project.Id && member.UserId == currentUserId, ct);
    }
}
