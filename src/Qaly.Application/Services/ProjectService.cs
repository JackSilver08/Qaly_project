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

        return Result.Success(project.ToDto());
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
            query = query.Where(project =>
                (project.OwnerId == currentUserId ||
                 project.Members.Any(member => member.UserId == currentUserId)) &&
                (project.OrganizationId == null ||
                 (project.Organization != null &&
                  project.Organization.IsActive &&
                  (project.Organization.OwnerId == currentUserId ||
                   project.Organization.Members.Any(member => member.UserId == currentUserId)))));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(p => p.Name.Contains(normalized) || (p.Description != null && p.Description.Contains(normalized)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = items.Select(item => item.ToDto()).ToList(),
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

        var query = ProjectDetailsQuery()
            .Where(p => p.OwnerId == userId || p.Members.Any(m => m.UserId == userId));

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = items.Select(item => item.ToDto()).ToList(),
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
        await _unitOfWork.SaveChangesAsync(ct);

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

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Create", nameof(Project), project.Id.ToString(), new { project.Name }, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Update", nameof(Project), project.Id.ToString(), dto, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Delete", nameof(Project), id.ToString(), new { project.Name }, ct);

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

        var memberRole = ProjectRoleRules.NormalizeProjectRole(role);
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
            await _unitOfWork.SaveChangesAsync(ct);
            await _auditLogService.LogAsync("UpdateMemberRole", nameof(Project), projectId.ToString(), new { userId, role = memberRole }, ct);
            return Result.Success();
        }

        await _memberRepo.AddAsync(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = memberRole,
            CanViewProjectTimeline = ProjectRoleRules.CanManageProject(memberRole),
            CanViewTaskRisk = ProjectRoleRules.CanManageProject(memberRole),
            CanNudgeAssignee = ProjectRoleRules.CanManageProject(memberRole),
            CanViewUnseenTaskSignal = ProjectRoleRules.CanManageProject(memberRole)
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AddMember", nameof(Project), projectId.ToString(), new { userId, role = memberRole }, ct);
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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("RemoveMember", nameof(Project), projectId.ToString(), new { userId }, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("UpdateMemberPermissions", nameof(Project), projectId.ToString(), new
        {
            userId,
            dto.CanViewProjectTimeline,
            dto.CanViewTaskRisk,
            dto.CanNudgeAssignee,
            dto.CanViewUnseenTaskSignal
        }, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("CreateLabel", nameof(Project), projectId.ToString(), new { label.Name, label.Color }, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("UpdateLabel", nameof(Project), projectId.ToString(), new { label.Id, label.Name, label.Color }, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("DeleteLabel", nameof(Project), projectId.ToString(), new { label.Id, label.Name }, ct);

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
                OrganizationOwnerId = project.Organization != null ? (Guid?)project.Organization.OwnerId : null
            })
            .FirstOrDefaultAsync(ct);

        if (projectInfo?.OrganizationId != null && !projectInfo.OrganizationIsActive)
        {
            return false;
        }

        if (projectInfo?.OrganizationId != null &&
            projectInfo.OrganizationOwnerId != currentUserId &&
            !await _organizationMemberRepo.GetQueryable().AnyAsync(member =>
                member.OrganizationId == projectInfo.OrganizationId.Value &&
                member.UserId == currentUserId,
                ct))
        {
            return false;
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

        var role = await GetProjectRoleAsync(projectId, currentUserId.Value, ct);
        if (ProjectRoleRules.CanManageProject(role))
        {
            return true;
        }

        return false;
    }

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
            query = query.Where(project =>
                (project.OwnerId == currentUserId ||
                 project.Members.Any(member => member.UserId == currentUserId)) &&
                (project.OrganizationId == null ||
                 (project.Organization != null &&
                  project.Organization.IsActive &&
                  (project.Organization.OwnerId == currentUserId ||
                   project.Organization.Members.Any(member => member.UserId == currentUserId)))));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.DeletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = items.Select(item => item.ToDto()).ToList(),
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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("Restore", nameof(Project), id.ToString(), new { project.Name }, ct);

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
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("HardDelete", nameof(Project), id.ToString(), new { project.Name }, ct);

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
            query = query.Where(project =>
                (project.OwnerId == currentUserId ||
                 project.Members.Any(member => member.UserId == currentUserId)) &&
                (project.OrganizationId == null ||
                 (project.Organization != null &&
                  project.Organization.IsActive &&
                  (project.Organization.OwnerId == currentUserId ||
                   project.Organization.Members.Any(member => member.UserId == currentUserId)))));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(p => p.Name.Contains(normalized) || (p.Description != null && p.Description.Contains(normalized)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.ArchivedAt ?? p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ProjectDto>
        {
            Items = items.Select(item => item.ToDto()).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }
}
