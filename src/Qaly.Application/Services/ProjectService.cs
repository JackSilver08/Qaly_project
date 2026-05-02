using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Mappings;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public ProjectService(
        IRepository<Project> projectRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<User> userRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IAuditLogService auditLogService)
    {
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
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
                project.OwnerId == currentUserId ||
                project.Members.Any(member => member.UserId == currentUserId));
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

        var project = dto.ToEntity();
        project.Name = dto.Name.Trim();
        project.OwnerId = currentUserId.Value;

        await _projectRepo.AddAsync(project, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _memberRepo.AddAsync(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = currentUserId.Value,
            Role = "Owner"
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

        if (!CanManageProject(project.OwnerId))
        {
            return Result.Forbidden<ProjectDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<ProjectDto>("Project name is required.");
        }

        dto.ApplyTo(project);
        project.Name = dto.Name.Trim();

        await _projectRepo.UpdateAsync(project, ct);
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

        if (!CanManageProject(project.OwnerId))
        {
            return Result.Failure("Access denied.", 403);
        }

        await _projectRepo.DeleteAsync(project, ct);
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

        if (!CanManageProject(project.OwnerId))
        {
            return Result.Failure("Access denied.", 403);
        }

        var userExists = await _userRepo.GetQueryable().AnyAsync(user => user.Id == userId && user.IsActive, ct);
        if (!userExists)
        {
            return Result.Failure("User was not found.", 404);
        }

        var memberRole = NormalizeMemberRole(role);
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
            Role = memberRole
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AddMember", nameof(Project), projectId.ToString(), new { userId, role = memberRole }, ct);
        await _notificationService.CreateAsync(userId, $"You were added to project \"{project.Name}\".", "ProjectInvite", project.Id, nameof(Project), ct);

        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
        {
            return Result.Failure("Project was not found.", 404);
        }

        if (!CanManageProject(project.OwnerId))
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

    private IQueryable<Project> ProjectDetailsQuery()
        => _projectRepo.GetQueryable()
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .Include(p => p.Tasks);

    private async Task<bool> CanAccessProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct)
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

        return await _memberRepo.GetQueryable()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == currentUserId, ct);
    }

    private bool CanManageProject(Guid ownerId)
        => IsAdmin() || _currentUserService.UserId == ownerId;

    private bool IsAdmin()
        => string.Equals(_currentUserService.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeMemberRole(string role)
    {
        var normalized = string.IsNullOrWhiteSpace(role) ? "Member" : role.Trim();
        return normalized switch
        {
            "Owner" or "Admin" or "Member" or "Viewer" => normalized,
            _ => "Member"
        };
    }
}
