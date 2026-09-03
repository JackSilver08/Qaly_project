using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qaly.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<Organization> _organizationRepo;
    private readonly IRepository<OrganizationMember> _organizationMemberRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<ProjectLabel> _labelRepo;
    private readonly IRepository<TaskAttachment> _attachmentRepo;
    private readonly IRepository<PhysicalFile> _physicalFileRepo;
    private readonly IRepository<VectorSyncOutbox> _outboxRepo;
    private readonly IRepository<ProjectRoleDefinition> _roleDefinitionRepo;
    private readonly IProjectRoleCatalog _roleCatalog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public ProjectService(
        IRepository<Project> projectRepo,
        IRepository<Organization> organizationRepo,
        IRepository<OrganizationMember> organizationMemberRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<User> userRepo,
        IRepository<ProjectLabel> labelRepo,
        IRepository<TaskAttachment> attachmentRepo,
        IRepository<PhysicalFile> physicalFileRepo,
        IRepository<VectorSyncOutbox> outboxRepo,
        IRepository<ProjectRoleDefinition> roleDefinitionRepo,
        IProjectRoleCatalog roleCatalog,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IAuditLogService auditLogService)
    {
        _projectRepo = projectRepo;
        _organizationRepo = organizationRepo;
        _organizationMemberRepo = organizationMemberRepo;
        _memberRepo = memberRepo;
        _userRepo = userRepo;
        _labelRepo = labelRepo;
        _attachmentRepo = attachmentRepo;
        _physicalFileRepo = physicalFileRepo;
        _outboxRepo = outboxRepo;
        _roleDefinitionRepo = roleDefinitionRepo;
        _roleCatalog = roleCatalog;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (!await HasActiveCurrentUserAsync(ct))
        {
            return Result.Forbidden<ProjectDto>();
        }

        var project = await ProjectDetailsQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project == null)
        {
            return Result.NotFound<ProjectDto>();
        }

        if (!await CanAccessProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Forbidden<ProjectDto>();
        }

        var customRoles = await LoadCustomRolesAsync([project], ct);
        return Result.Success(ToDtoWithPermissions(project, customRoles));
    }

    public async Task<Result<PagedResult<ProjectDto>>> GetAllAsync(int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default)
    {
        if (!await HasActiveCurrentUserAsync(ct))
        {
            return Result.Forbidden<PagedResult<ProjectDto>>();
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<ProjectDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ProjectDetailsQuery();

        if (!IsAdmin())
        {
            query = ApplyAccessibleProjectFilter(query, currentUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(p => p.Name.Contains(normalized) || (p.Description != null && p.Description.Contains(normalized)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customRoles = await LoadCustomRolesAsync(items, ct);
        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = items.Select(item => ToDtoWithPermissions(item, customRoles)).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<PagedResult<ProjectDto>>> GetByUserAsync(Guid userId, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        if (!await HasActiveCurrentUserAsync(ct))
        {
            return Result.Forbidden<PagedResult<ProjectDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (!IsAdmin() && _currentUserService.UserId != userId)
        {
            return Result.Forbidden<PagedResult<ProjectDto>>();
        }

        var query = ApplyAccessibleProjectFilter(ProjectDetailsQuery(), userId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customRoles = await LoadCustomRolesAsync(items, ct);
        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = items.Select(item => ToDtoWithPermissions(item, customRoles)).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<ProjectDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<ProjectDto>("Project name is required.");
        }

        var owner = await _userRepo.GetByIdAsync(currentUserId.Value, ct);
        if (owner == null || !owner.IsActive)
        {
            return Result.Failure<ProjectDto>(
                "Your session no longer matches an active Qaly user. Please sign in again.",
                401);
        }

        var project = dto.ToEntity();
        project.Name = dto.Name.Trim();
        project.Code = await GenerateUniqueCodeAsync(dto.Code, dto.Name, ct);
        project.LogoUrl = NormalizeOptional(dto.LogoUrl);
        project.OwnerId = currentUserId.Value;

        if (dto.OrganizationId.HasValue)
        {
            var organization = await _organizationRepo.GetByIdAsync(dto.OrganizationId.Value, ct);
            if (organization == null)
            {
                return Result.Failure<ProjectDto>("Organization was not found.", 404);
            }

            if (!organization.IsActive)
            {
                return Result.Failure<ProjectDto>("Organization is inactive.", 400);
            }

            if (!await CanAccessOrganizationAsync(dto.OrganizationId.Value, organization.OwnerId, ct))
            {
                return Result.Forbidden<ProjectDto>();
            }

            project.OrganizationId = dto.OrganizationId.Value;
        }

        await _projectRepo.AddAsync(project, ct);
        await AddToOutboxAsync("ProjectCreated", new { Id = project.Id }, ct);
        await _memberRepo.AddAsync(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = currentUserId.Value,
            Role = "Owner",
            CanViewProjectTimeline = true,
            CanViewTaskRisk = true,
            CanNudgeAssignee = true,
            CanViewUnseenTaskSignal = true
        }, ct);

        // Project, owner membership and its durable sync signal are one canonical graph.
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Create",
            nameof(Project),
            project.Id.ToString(),
            new { project.Name },
            ct);

        return await GetByIdAsync(project.Id, ct);
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(id, ct);
        if (project == null)
        {
            return Result.NotFound<ProjectDto>();
        }

        if (!await CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Forbidden<ProjectDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<ProjectDto>("Project name is required.");
        }

        dto.ApplyTo(project);
        project.Name = dto.Name.Trim();
        project.Code = await GenerateUniqueCodeAsync(dto.Code, dto.Name, ct, id);
        project.LogoUrl = NormalizeOptional(dto.LogoUrl);

        // Track archive timestamp
        if (project.Status == "Archived" && project.ArchivedAt == null)
        {
            project.ArchivedAt = DateTimeOffset.UtcNow;
        }
        else if (project.Status != "Archived" && project.ArchivedAt != null)
        {
            project.ArchivedAt = null;
        }

        if (dto.OrganizationId.HasValue)
        {
            var organization = await _organizationRepo.GetByIdAsync(dto.OrganizationId.Value, ct);
            if (organization == null)
            {
                return Result.Failure<ProjectDto>("Organization was not found.", 404);
            }

            if (!organization.IsActive)
            {
                return Result.Failure<ProjectDto>("Organization is inactive.", 400);
            }

            if (!await CanManageOrganizationAsync(dto.OrganizationId.Value, organization.OwnerId, ct))
            {
                return Result.Forbidden<ProjectDto>();
            }

            project.OrganizationId = dto.OrganizationId.Value;
        }

        await _projectRepo.UpdateAsync(project, ct);
        await AddToOutboxAsync("ProjectUpdated", new { Id = project.Id }, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Update",
            nameof(Project),
            project.Id.ToString(),
            dto,
            ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(id, ct);
        if (project == null)
        {
            return Result.Failure("Project was not found.", 404);
        }

        if (!await CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        await _projectRepo.DeleteAsync(project, ct);
        await AddToOutboxAsync("ProjectDeleted", new { Id = project.Id }, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Delete",
            nameof(Project),
            id.ToString(),
            new { project.Name },
            ct);

        return Result.Success();
    }

    public async Task<Result> AddMemberAsync(Guid projectId, Guid userId, string role, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return Result.Failure("Project was not found.", 404);
        }

        if (!await CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        var userExists = await _userRepo.GetQueryable().AnyAsync(user => user.Id == userId && user.IsActive, ct);
        if (!userExists)
        {
            return Result.Failure("User was not found.", 404);
        }

        // Accepts a built-in role or one this organization defined for itself. Unknown values are
        // rejected rather than silently downgraded to Member.
        var resolvedRole = await _roleCatalog.ResolveAssignableAsync(role, project.OrganizationId, ct);
        if (resolvedRole == null || string.Equals(resolvedRole.Key, ProjectRoleRules.Owner, StringComparison.Ordinal))
        {
            return Result.Failure(
                $"'{role}' is not an assignable project role. Allowed built-in roles: {string.Join(", ", ProjectRoleRules.AssignableRoles)}.",
                400);
        }

        var memberRole = resolvedRole.Key;

        var existingMember = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(member => member.ProjectId == projectId && member.UserId == userId, ct);

        if (existingMember != null)
        {
            if (project.OwnerId == userId && memberRole != "Owner")
            {
                return Result.Failure("Project owner role cannot be changed via this method.", 400);
            }

            existingMember.Role = memberRole;
            await _memberRepo.UpdateAsync(existingMember, ct);
            var backfilledOrganization = await EnsureOrganizationMembershipAsync(project, userId, ct);
            await StageOrganizationBackfillAsync(backfilledOrganization, project, userId, ct);
            await _unitOfWork.SaveChangesWithAuditAsync(
                _auditLogService,
                "UpdateMemberRole",
                nameof(Project),
                projectId.ToString(),
                new { userId, role = memberRole },
                ct);
            return Result.Success();
        }

        await _memberRepo.AddAsync(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = memberRole,
            CanViewProjectTimeline = ProjectRoleRules.CanManageProject(resolvedRole.BaseRole),
            CanViewTaskRisk = ProjectRoleRules.CanManageProject(resolvedRole.BaseRole),
            CanNudgeAssignee = ProjectRoleRules.CanManageProject(resolvedRole.BaseRole),
            CanViewUnseenTaskSignal = ProjectRoleRules.CanManageProject(resolvedRole.BaseRole)
        }, ct);

        var organizationBackfilled = await EnsureOrganizationMembershipAsync(project, userId, ct);
        await StageOrganizationBackfillAsync(organizationBackfilled, project, userId, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "AddMember",
            nameof(Project),
            projectId.ToString(),
            new { userId, role = memberRole },
            ct);
        await _notificationService.CreateAsync(
            userId,
            $"You were added to project \"{project.Name}\".",
            "ProjectInvite",
            "success",
            project.Id,
            nameof(Project),
            $"project:{project.Id}:invite:{userId}",
            ct);

        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return Result.Failure("Project was not found.", 404);
        }

        if (!await CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        if (project.OwnerId == userId)
        {
            return Result.Failure("Project owner cannot be removed from the project.", 400);
        }

        var member = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

        if (member == null)
        {
            return Result.Failure("Project member was not found.", 404);
        }

        await _memberRepo.DeleteAsync(member, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "RemoveMember",
            nameof(Project),
            projectId.ToString(),
            new { userId },
            ct);

        return Result.Success();
    }

    public async Task<Result> UpdateMemberPermissionsAsync(Guid projectId, Guid userId, UpdateProjectMemberPermissionsDto dto, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return Result.Failure("Không tìm thấy dự án.", 404);
        }

        if (!await CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Failure("Bạn không có quyền cấu hình quyền thành viên trong dự án này.", 403);
        }

        var member = await _memberRepo.GetQueryable()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.UserId == userId, ct);

        if (member == null)
        {
            return Result.Failure("Không tìm thấy thành viên trong dự án.", 404);
        }

        if (project.OwnerId == userId)
        {
            return Result.Failure("Không thể thu hồi quyền timeline của người tạo dự án.", 400);
        }

        member.CanViewProjectTimeline = dto.CanViewProjectTimeline;
        member.CanViewTaskRisk = dto.CanViewTaskRisk;
        member.CanNudgeAssignee = dto.CanNudgeAssignee;
        member.CanViewUnseenTaskSignal = dto.CanViewUnseenTaskSignal;

        await _memberRepo.UpdateAsync(member, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "UpdateMemberPermissions",
            nameof(Project),
            projectId.ToString(),
            new
            {
                userId,
                dto.CanViewProjectTimeline,
                dto.CanViewTaskRisk,
                dto.CanNudgeAssignee,
                dto.CanViewUnseenTaskSignal
            },
            ct);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ProjectLabelDto>>> GetLabelsAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return Result.NotFound<IReadOnlyList<ProjectLabelDto>>();
        }

        if (!await CanAccessProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Forbidden<IReadOnlyList<ProjectLabelDto>>();
        }

        var labels = await _labelRepo.GetQueryable()
            .AsNoTracking()
            .Where(label => label.ProjectId == projectId)
            .OrderBy(label => label.Name)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<ProjectLabelDto>>(labels.Select(label => label.ToDto()).ToList());
    }

    public async Task<Result<ProjectLabelDto>> CreateLabelAsync(Guid projectId, CreateProjectLabelDto dto, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return Result.NotFound<ProjectLabelDto>();
        }

        if (!await CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Forbidden<ProjectLabelDto>();
        }

        var validation = ValidateLabel(dto.Name, dto.Color);
        if (!validation.IsSuccess)
        {
            return Result.Failure<ProjectLabelDto>(validation.Error!, validation.StatusCode);
        }

        var exists = await _labelRepo.GetQueryable()
            .AnyAsync(label => label.ProjectId == projectId && label.Name == dto.Name.Trim(), ct);
        if (exists)
        {
            return Result.Failure<ProjectLabelDto>("Label name already exists in this project.", 409);
        }

        var label = new ProjectLabel
        {
            ProjectId = projectId,
            Name = dto.Name.Trim(),
            Color = NormalizeHexColor(dto.Color)
        };

        await _labelRepo.AddAsync(label, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "CreateLabel",
            nameof(Project),
            projectId.ToString(),
            new { label.Name, label.Color },
            ct);

        return Result.Created(label.ToDto());
    }

    public async Task<Result<ProjectLabelDto>> UpdateLabelAsync(Guid projectId, Guid labelId, UpdateProjectLabelDto dto, CancellationToken ct = default)
    {
        var label = await _labelRepo.GetQueryable()
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == labelId && item.ProjectId == projectId, ct);
        if (label == null)
        {
            return Result.NotFound<ProjectLabelDto>();
        }

        if (!await CanManageProjectAsync(projectId, label.Project.OwnerId, ct))
        {
            return Result.Forbidden<ProjectLabelDto>();
        }

        var validation = ValidateLabel(dto.Name, dto.Color);
        if (!validation.IsSuccess)
        {
            return Result.Failure<ProjectLabelDto>(validation.Error!, validation.StatusCode);
        }

        var newName = dto.Name.Trim();
        var duplicate = await _labelRepo.GetQueryable()
            .AnyAsync(item => item.ProjectId == projectId && item.Id != labelId && item.Name == newName, ct);
        if (duplicate)
        {
            return Result.Failure<ProjectLabelDto>("Label name already exists in this project.", 409);
        }

        label.Name = newName;
        label.Color = NormalizeHexColor(dto.Color);
        await _labelRepo.UpdateAsync(label, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "UpdateLabel",
            nameof(Project),
            projectId.ToString(),
            new { label.Id, label.Name, label.Color },
            ct);

        return Result.Success(label.ToDto());
    }

    public async Task<Result> DeleteLabelAsync(Guid projectId, Guid labelId, CancellationToken ct = default)
    {
        var label = await _labelRepo.GetQueryable()
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == labelId && item.ProjectId == projectId, ct);
        if (label == null)
        {
            return Result.NotFound();
        }

        if (!await CanManageProjectAsync(projectId, label.Project.OwnerId, ct))
        {
            return Result.Forbidden();
        }

        await _labelRepo.DeleteAsync(label, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "DeleteLabel",
            nameof(Project),
            projectId.ToString(),
            new { label.Id, label.Name },
            ct);

        return Result.Success();
    }

    private async Task AddToOutboxAsync(string eventType, object payload, CancellationToken ct)
    {
        var message = new VectorSyncOutbox
        {
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload)
        };
        await _outboxRepo.AddAsync(message, ct);
    }

    /// <summary>
    /// Maps a project and attaches what the current user may do with it, so the UI renders controls
    /// from the server's answer rather than re-deriving permissions from the role string.
    /// Relies on <see cref="ProjectDetailsQuery"/> having loaded members.
    /// </summary>
    /// <param name="customRoles">
    /// Organization and custom role key to its inherited built-in role and label, pre-loaded by
    /// <see cref="LoadCustomRolesAsync"/>. Built-in roles are absent from this map.
    /// </param>
    private ProjectDto ToDtoWithPermissions(
        Project project,
        IReadOnlyDictionary<(Guid OrganizationId, string Key), CustomRoleInfo> customRoles)
    {
        var currentUserId = _currentUserService.UserId;
        var storedRole = project.Members?
            .FirstOrDefault(member => member.UserId == currentUserId)?
            .Role;

        CustomRoleInfo? custom = null;
        var effectiveRole = storedRole;
        if (storedRole != null
            && project.OrganizationId.HasValue
            && customRoles.TryGetValue((project.OrganizationId.Value, storedRole), out var match))
        {
            custom = match;
            effectiveRole = match.BaseRole;
        }

        var permissions = ProjectPermissionRules.Resolve(
            effectiveRole,
            isOwner: currentUserId != null && project.OwnerId == currentUserId,
            isSystemAdmin: IsAdmin(),
            isOrganizationManager: IsCurrentUserOrganizationManager(project));

        // Show the organization's own label while permissions stay driven by the inherited role.
        if (custom != null)
        {
            permissions = permissions with { Role = storedRole!, RoleLabel = custom.DisplayName };
        }

        return project.ToDto() with { Permissions = permissions };
    }

    private sealed record CustomRoleInfo(string BaseRole, string DisplayName);

    /// <summary>
    /// Loads the custom roles of the organizations owning the given projects in one query, so
    /// permission mapping does not issue a query per project.
    /// </summary>
    private async Task<IReadOnlyDictionary<(Guid OrganizationId, string Key), CustomRoleInfo>> LoadCustomRolesAsync(
        IEnumerable<Project> projects,
        CancellationToken ct)
    {
        var organizationIds = projects
            .Select(project => project.OrganizationId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (organizationIds.Count == 0)
        {
            return new Dictionary<(Guid OrganizationId, string Key), CustomRoleInfo>();
        }

        var definitions = await _roleDefinitionRepo.GetQueryable()
            .AsNoTracking()
            .Where(definition => organizationIds.Contains(definition.OrganizationId))
            .Select(definition => new
            {
                definition.OrganizationId,
                definition.Key,
                definition.BaseRole,
                definition.DisplayName
            })
            .ToListAsync(ct);

        return definitions
            .GroupBy(definition => (definition.OrganizationId, definition.Key))
            .ToDictionary(
                group => group.Key,
                group => new CustomRoleInfo(
                    ProjectRoleRules.NormalizeProjectRole(group.First().BaseRole),
                    group.First().DisplayName));
    }

    private IQueryable<Project> ProjectDetailsQuery()
    {
        var currentUserId = _currentUserService.UserId;
        var canViewEveryPrivateTask = IsAdmin();
        return _projectRepo.GetQueryable()
            .Include(p => p.Owner)
            .Include(p => p.Organization)
                .ThenInclude(o => o!.Members)
            .Include(p => p.Members)
            .Include(p => p.Labels)
            .Include(p => p.Tasks.Where(task =>
                canViewEveryPrivateTask ||
                !task.IsPrivate ||
                task.ReporterId == currentUserId ||
                task.AssigneeId == currentUserId ||
                task.Assignees.Any(assignment => assignment.UserId == currentUserId) ||
                task.Project.OwnerId == currentUserId));
    }

    /// <summary>
    /// Canonical Project visibility for list-style queries. Organization Owner/Admin has portfolio
    /// authority; other Organization roles still require an explicit Project ownership/membership
    /// row. An inactive Organization closes every non-system-admin path.
    /// </summary>
    private static IQueryable<Project> ApplyAccessibleProjectFilter(IQueryable<Project> query, Guid userId)
        => query.Where(project =>
            project.OrganizationId == null
                ? project.OwnerId == userId || project.Members.Any(member => member.UserId == userId)
                : project.Organization != null &&
                  project.Organization.IsActive &&
                  (
                      project.Organization.OwnerId == userId ||
                      project.Organization.Members.Any(member =>
                          member.UserId == userId &&
                          (member.Role == OrganizationRoleRules.OrganizationAdmin ||
                           member.Role == "Admin" ||
                           member.Role == "Manager")) ||
                      (
                          (project.OwnerId == userId || project.Members.Any(member => member.UserId == userId)) &&
                          project.Organization.Members.Any(member => member.UserId == userId)
                      )
                  ));

    private bool IsCurrentUserOrganizationManager(Project project)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue || project.Organization == null || !project.Organization.IsActive)
        {
            return false;
        }

        if (project.Organization.OwnerId == currentUserId.Value)
        {
            return true;
        }

        var role = project.Organization.Members
            .FirstOrDefault(member => member.UserId == currentUserId.Value)?.Role;
        return OrganizationRoleRules.CanManageOrganization(role);
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        if (!await HasActiveCurrentUserAsync(ct))
        {
            return false;
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin())
        {
            return true;
        }

        if (!await IsProjectOrganizationActiveAsync(projectId, ct))
        {
            return false;
        }

        var projectInfo = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                project.OrganizationId,
                OrganizationIsActive = project.Organization != null && project.Organization.IsActive,
                OrganizationOwnerId = project.Organization != null ? (Guid?)project.Organization.OwnerId : null,
                OrganizationRole = project.Organization == null
                    ? null
                    : project.Organization.Members
                        .Where(member => member.UserId == currentUserId)
                        .Select(member => member.Role)
                        .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (projectInfo?.OrganizationId != null && !projectInfo.OrganizationIsActive)
        {
            return false;
        }

        if (projectInfo?.OrganizationId != null &&
            projectInfo.OrganizationOwnerId != currentUserId &&
            string.IsNullOrWhiteSpace(projectInfo.OrganizationRole))
        {
            return false;
        }

        if (projectInfo?.OrganizationId != null &&
            (projectInfo.OrganizationOwnerId == currentUserId ||
             OrganizationRoleRules.CanManageOrganization(projectInfo.OrganizationRole)))
        {
            return true;
        }

        if (ownerId == currentUserId)
        {
            return true;
        }

        var isProjectMember = await _memberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == currentUserId, ct);
        if (isProjectMember)
        {
            return true;
        }

        return false;
    }

    private async Task<bool> CanManageProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
    {
        if (!await HasActiveCurrentUserAsync(ct))
        {
            return false;
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin())
        {
            return true;
        }

        if (!await CanAccessProjectAsync(projectId, ownerId, ct))
        {
            return false;
        }

        if (ownerId == currentUserId)
        {
            return true;
        }

        var organizationAuthority = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .Where(project => project.Id == projectId && project.Organization != null)
            .Select(project => new
            {
                IsOwner = project.Organization!.OwnerId == currentUserId,
                Role = project.Organization.Members
                    .Where(member => member.UserId == currentUserId)
                    .Select(member => member.Role)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
        if (organizationAuthority != null &&
            (organizationAuthority.IsOwner || OrganizationRoleRules.CanManageOrganization(organizationAuthority.Role)))
        {
            return true;
        }

        var role = await GetProjectRoleAsync(projectId, currentUserId.Value, ct);
        var organizationId = await _projectRepo.GetQueryable()
            .Where(project => project.Id == projectId)
            .Select(project => project.OrganizationId)
            .FirstOrDefaultAsync(ct);
        var resolvedRole = await _roleCatalog.ResolveAsync(role, organizationId, ct);
        if (resolvedRole != null && ProjectRoleRules.CanManageProject(resolvedRole.BaseRole))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// A project member must also belong to the owning organization. Project reads are filtered by
    /// organization membership, so without this row a user added to an organization-scoped project
    /// sees an empty project list and gets 403 on the project detail.
    /// The grant is the lowest organization role and never overwrites an existing membership.
    /// </summary>
    private async Task<bool> EnsureOrganizationMembershipAsync(Project project, Guid userId, CancellationToken ct)
    {
        if (project.OrganizationId is not Guid organizationId)
        {
            return false;
        }

        var organizationOwnerId = await _organizationRepo.GetQueryable()
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => (Guid?)organization.OwnerId)
            .FirstOrDefaultAsync(ct);

        if (organizationOwnerId == null || organizationOwnerId == userId)
        {
            return false;
        }

        var alreadyMember = await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == organizationId && member.UserId == userId, ct);
        if (alreadyMember)
        {
            return false;
        }

        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = OrganizationRoleRules.Member
        }, ct);

        return true;
    }

    private Task StageOrganizationBackfillAsync(bool backfilled, Project project, Guid userId, CancellationToken ct)
        => backfilled && project.OrganizationId.HasValue
            ? _auditLogService.StageAsync(
                "AddOrganizationMemberViaProject",
                nameof(Organization),
                project.OrganizationId.Value.ToString(),
                new { userId, role = OrganizationRoleRules.Member, projectId = project.Id },
                ct)
            : Task.CompletedTask;

    private async Task<string?> GetProjectRoleAsync(Guid projectId, Guid userId, CancellationToken ct)
        => await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> IsProjectOrganizationActiveAsync(Guid projectId, CancellationToken ct)
        => await _projectRepo.GetQueryable()
            .Where(project => project.Id == projectId)
            .Select(project => project.OrganizationId == null ||
                (project.Organization != null && project.Organization.IsActive))
            .FirstOrDefaultAsync(ct);

    private async Task<bool> HasActiveCurrentUserAsync(CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        return await _userRepo.GetQueryable()
            .AnyAsync(user => user.Id == currentUserId.Value && user.IsActive, ct);
    }

    private bool IsAdmin()
        => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

    private async Task<bool> CanAccessOrganizationAsync(Guid organizationId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin() || ownerId == currentUserId)
        {
            return true;
        }

        return await _organizationMemberRepo.GetQueryable()
            .AnyAsync(member => member.OrganizationId == organizationId && member.UserId == currentUserId, ct);
    }

    private async Task<bool> CanManageOrganizationAsync(Guid organizationId, Guid ownerId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin() || ownerId == currentUserId)
        {
            return true;
        }

        var role = await _organizationMemberRepo.GetQueryable()
            .Where(member => member.OrganizationId == organizationId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);

        return OrganizationRoleRules.CanManageOrganization(role);
    }

    private async Task<string> GenerateUniqueCodeAsync(string? requestedCode, string name, CancellationToken ct, Guid? currentProjectId = null)
    {
        var baseCode = Slugify(string.IsNullOrWhiteSpace(requestedCode) ? name : requestedCode);
        if (string.IsNullOrWhiteSpace(baseCode))
        {
            baseCode = "project";
        }

        var candidate = baseCode;
        var suffix = 2;
        while (await _projectRepo.GetQueryable().AnyAsync(project => project.Code == candidate && project.Id != currentProjectId, ct))
        {
            candidate = $"{baseCode}-{suffix++}";
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", string.Empty, RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"[\s-]+", "-", RegexOptions.CultureInvariant).Trim('-');
        return normalized.Length > 80 ? normalized[..80].Trim('-') : normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeHexColor(string color)
        => string.IsNullOrWhiteSpace(color) ? "#64748B" : color.Trim().ToUpperInvariant();

    private static Result ValidateLabel(string name, string color)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Label name is required.");
        }

        if (name.Trim().Length > 80)
        {
            return Result.Failure("Label name must be 80 characters or fewer.");
        }

        var normalized = NormalizeHexColor(color);
        if (!Regex.IsMatch(normalized, "^#[0-9A-F]{6}$", RegexOptions.CultureInvariant))
        {
            return Result.Failure("Label color must be a hex color like #FF0000.");
        }

        return Result.Success();
    }

    public async Task<Result<PagedResult<ProjectDto>>> GetTrashAsync(int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<ProjectDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ProjectDetailsQuery()
            .IgnoreQueryFilters()
            .Where(project => project.IsDeleted);

        if (!IsAdmin())
        {
            query = ApplyAccessibleProjectFilter(query, currentUserId.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.DeletedAt)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customRoles = await LoadCustomRolesAsync(items, ct);
        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = items.Select(item => ToDtoWithPermissions(item, customRoles)).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    public async Task<Result> RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project == null)
        {
            return Result.Failure("Project was not found.", 404);
        }

        if (!await CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        project.IsDeleted = false;
        project.DeletedAt = null;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        await _projectRepo.UpdateAsync(project, ct);
        await AddToOutboxAsync("ProjectRestored", new { Id = project.Id }, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "Restore",
            nameof(Project),
            id.ToString(),
            new { project.Name },
            ct);

        return Result.Success();
    }

    public async Task<Result> HardDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project == null)
        {
            return Result.Failure("Project was not found.", 404);
        }

        if (!await CanManageProjectAsync(project.Id, project.OwnerId, ct))
        {
            return Result.Failure("Access denied.", 403);
        }

        await HardDeleteProjectAttachmentsAsync(project.Id, ct);

        await _projectRepo.HardDeleteAsync(project, ct);
        await AddToOutboxAsync("ProjectHardDeleted", new { Id = project.Id }, ct);
        await _unitOfWork.SaveChangesWithAuditAsync(
            _auditLogService,
            "HardDelete",
            nameof(Project),
            id.ToString(),
            new { project.Name },
            ct);

        return Result.Success();
    }

    private async Task HardDeleteProjectAttachmentsAsync(Guid projectId, CancellationToken ct)
    {
        var attachments = await GetProjectAttachmentsForHardDeleteAsync(projectId, ct);
        var releaseCounts = attachments
            .Where(attachment => attachment.PhysicalFileId != Guid.Empty)
            .GroupBy(attachment => attachment.PhysicalFileId)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var attachment in attachments)
        {
            await _attachmentRepo.HardDeleteAsync(attachment, ct);
        }

        foreach (var (physicalFileId, releaseCount) in releaseCounts)
        {
            var physicalFile = await _physicalFileRepo.GetQueryable()
                .FirstOrDefaultAsync(file => file.Id == physicalFileId, ct);

            if (physicalFile == null)
            {
                continue;
            }

            physicalFile.ReferenceCount -= releaseCount;
            if (physicalFile.ReferenceCount > 0)
            {
                await _physicalFileRepo.UpdateAsync(physicalFile, ct);
                continue;
            }

            try
            {
                await _fileStorageService.DeleteAsync(physicalFile.FilePath, ct);
            }
            catch
            {
                // DB cleanup must still proceed; missing files are handled as already removed.
            }

            await _physicalFileRepo.HardDeleteAsync(physicalFile, ct);
        }
    }

    private async Task<IReadOnlyList<TaskAttachment>> GetProjectAttachmentsForHardDeleteAsync(Guid projectId, CancellationToken ct)
        => await _attachmentRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Include(attachment => attachment.PhysicalFile)
            .Where(attachment =>
                attachment.ProjectId == projectId ||
                (attachment.TaskItem != null && attachment.TaskItem.ProjectId == projectId) ||
                (attachment.Comment != null && attachment.Comment.TaskItem.ProjectId == projectId))
            .ToListAsync(ct);

    public async Task<Result<PagedResult<ProjectDto>>> GetArchivedAsync(int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Result.Forbidden<PagedResult<ProjectDto>>();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ProjectDetailsQuery()
            .Where(project => project.Status == "Archived");

        if (!IsAdmin())
        {
            query = ApplyAccessibleProjectFilter(query, currentUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(p => p.Name.Contains(normalized) || (p.Description != null && p.Description.Contains(normalized)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.ArchivedAt ?? p.UpdatedAt)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customRoles = await LoadCustomRolesAsync(items, ct);
        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = items.Select(item => ToDtoWithPermissions(item, customRoles)).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }
}
