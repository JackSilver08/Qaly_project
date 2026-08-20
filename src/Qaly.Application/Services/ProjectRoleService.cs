using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class ProjectRoleService : IProjectRoleService
{
    private readonly IRepository<ProjectCustomRole> _roleRepo;
    private readonly IRepository<ProjectMemberRoleHistory> _historyRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<SystemModulePermission> _systemPermRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IProjectRoleCatalog _roleCatalog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public ProjectRoleService(
        IRepository<ProjectCustomRole> roleRepo,
        IRepository<ProjectMemberRoleHistory> historyRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<Project> projectRepo,
        IRepository<SystemModulePermission> systemPermRepo,
        IRepository<User> userRepo,
        IProjectRoleCatalog roleCatalog,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _roleRepo = roleRepo;
        _historyRepo = historyRepo;
        _memberRepo = memberRepo;
        _projectRepo = projectRepo;
        _systemPermRepo = systemPermRepo;
        _userRepo = userRepo;
        _roleCatalog = roleCatalog;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<Result<List<ProjectCustomRoleDto>>> GetCustomRolesAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, ct))
        {
            return Result.Forbidden<List<ProjectCustomRoleDto>>();
        }

        var roles = await _roleRepo.GetQueryable()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var dtos = roles.Select(r => new ProjectCustomRoleDto(
            r.Id, r.ProjectId, r.Name, r.Description, r.ColorCode, r.IsSystemDefault, r.PermissionMatrixJson, r.CreatedAt
        )).ToList();

        return Result.Success(dtos);
    }

    public async Task<Result<ProjectCustomRoleDto>> CreateCustomRoleAsync(Guid projectId, CreateProjectCustomRoleDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden<ProjectCustomRoleDto>();

        if (!await CanManageProjectAsync(projectId, ct))
        {
            return Result.Forbidden<ProjectCustomRoleDto>();
        }

        if (!await _projectRepo.GetQueryable().AnyAsync(project => project.Id == projectId, ct))
        {
            return Result.NotFound<ProjectCustomRoleDto>();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure<ProjectCustomRoleDto>("Role name is required.");
        }

        var role = new ProjectCustomRole
        {
            ProjectId = projectId,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            ColorCode = dto.ColorCode ?? "#8B5CF6",
            PermissionMatrixJson = dto.PermissionMatrixJson ?? "{}"
        };

        await _roleRepo.AddAsync(role, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("CreateCustomRole", nameof(ProjectCustomRole), role.Id.ToString(), new { role.Name }, ct);

        return Result.Created(new ProjectCustomRoleDto(
            role.Id, role.ProjectId, role.Name, role.Description, role.ColorCode, role.IsSystemDefault, role.PermissionMatrixJson, role.CreatedAt
        ));
    }

    public async Task<Result<RoleAssignConflictCheckResultDto>> CheckRoleAssignConflictsAsync(Guid projectId, Guid memberId, AssignProjectMemberRoleDto dto, CancellationToken ct = default)
    {
        if (!await CanManageProjectAsync(projectId, ct))
        {
            return Result.Forbidden<RoleAssignConflictCheckResultDto>();
        }

        var member = await _memberRepo.GetQueryable()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId, ct);

        if (member == null) return Result.NotFound<RoleAssignConflictCheckResultDto>();

        var activeRoleRecord = await _historyRepo.GetQueryable()
            .Include(h => h.Role)
            .FirstOrDefaultAsync(h => h.ProjectMemberId == memberId && h.EndDate == null, ct);

        bool hasOverlap = activeRoleRecord != null;
        string? activeRoleName = activeRoleRecord?.Role?.Name;
        DateTimeOffset? activeStartDate = activeRoleRecord?.StartDate;

        // Check System Role Conflict
        var userSystemRole = member.User?.Role ?? "User";
        var systemPerm = await _systemPermRepo.GetQueryable()
            .FirstOrDefaultAsync(p => p.SystemRole == userSystemRole && p.ModuleKey == "AiHub", ct);

        bool hasSystemConflict = systemPerm != null && (!systemPerm.IsAllowed || systemPerm.AiTier == "Restricted");

        var result = new RoleAssignConflictCheckResultDto(
            hasOverlap,
            activeRoleName,
            activeStartDate,
            hasSystemConflict,
            userSystemRole,
            systemPerm?.AiTier ?? "Full",
            hasOverlap || hasSystemConflict ? "Requires user confirmation" : "No conflicts"
        );

        return Result.Success(result);
    }

    public async Task<Result<ProjectMemberRoleHistoryDto>> AssignRoleAsync(Guid projectId, Guid memberId, AssignProjectMemberRoleDto dto, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden<ProjectMemberRoleHistoryDto>();

        if (!await CanManageProjectAsync(projectId, ct))
        {
            return Result.Forbidden<ProjectMemberRoleHistoryDto>();
        }

        var member = await _memberRepo.GetQueryable()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId, ct);

        if (member == null) return Result.NotFound<ProjectMemberRoleHistoryDto>();

        var role = await _roleRepo.GetByIdAsync(dto.RoleId, ct);
        if (role == null || role.ProjectId != projectId)
        {
            return Result.Failure<ProjectMemberRoleHistoryDto>("Project role not found.", 404);
        }

        // Close any existing active role record (EndDate == null) -> Enforces 1 Active Role per Member
        var activeHistory = await _historyRepo.GetQueryable()
            .Where(h => h.ProjectMemberId == memberId && h.EndDate == null)
            .ToListAsync(ct);

        foreach (var history in activeHistory)
        {
            history.EndDate = dto.StartDate;
            history.UpdatedAt = DateTimeOffset.UtcNow;
            await _historyRepo.UpdateAsync(history, ct);
        }

        // Insert new active role record
        var newHistory = new ProjectMemberRoleHistory
        {
            ProjectMemberId = memberId,
            RoleId = dto.RoleId,
            PhaseName = dto.PhaseName?.Trim(),
            StartDate = dto.StartDate,
            EndDate = null, // Active
            ReasonOrNote = dto.ReasonOrNote?.Trim(),
            AssignedByUserId = currentUserId.Value
        };

        await _historyRepo.AddAsync(newHistory, ct);

        // Update current member role string
        member.Role = role.Name;
        await _memberRepo.UpdateAsync(member, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("AssignMemberRole", nameof(ProjectMemberRoleHistory), newHistory.Id.ToString(), new { memberId, role.Name, dto.PhaseName }, ct);

        var assigner = await _userRepo.GetByIdAsync(currentUserId.Value, ct);

        return Result.Success(new ProjectMemberRoleHistoryDto(
            newHistory.Id,
            newHistory.ProjectMemberId,
            newHistory.RoleId,
            role.Name,
            role.ColorCode,
            newHistory.PhaseName,
            newHistory.StartDate,
            newHistory.EndDate,
            true,
            newHistory.ReasonOrNote,
            newHistory.AssignedByUserId,
            assigner?.FullName ?? "Admin"
        ));
    }

    public async Task<Result<List<ProjectMemberRoleHistoryDto>>> GetMemberRoleHistoryAsync(Guid projectId, Guid memberId, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return Result.Forbidden<List<ProjectMemberRoleHistoryDto>>();

        var member = await _memberRepo.GetQueryable()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId, ct);

        if (member == null) return Result.NotFound<List<ProjectMemberRoleHistoryDto>>();

        // PRIVACY ENFORCEMENT POLICY:
        // Self view OR Project Owner / Manager / System Admin view
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        bool isSelf = member.UserId == currentUserId.Value;
        bool isOwnerOrAdmin = project != null && (project.OwnerId == currentUserId.Value || IsAdmin());

        if (!isSelf && !isOwnerOrAdmin)
        {
            return Result.Forbidden<List<ProjectMemberRoleHistoryDto>>();
        }

        var histories = await _historyRepo.GetQueryable()
            .Include(h => h.Role)
            .Include(h => h.AssignedByUser)
            .Where(h => h.ProjectMemberId == memberId)
            .OrderByDescending(h => h.StartDate)
            .ToListAsync(ct);

        var dtos = histories.Select(h => new ProjectMemberRoleHistoryDto(
            h.Id,
            h.ProjectMemberId,
            h.RoleId,
            h.Role?.Name ?? "Custom Role",
            h.Role?.ColorCode ?? "#8B5CF6",
            h.PhaseName,
            h.StartDate,
            h.EndDate,
            h.EndDate == null,
            h.ReasonOrNote,
            h.AssignedByUserId,
            h.AssignedByUser?.FullName ?? "System Admin"
        )).ToList();

        return Result.Success(dtos);
    }

    public async Task<Result<List<SystemModulePermissionDto>>> GetSystemModulePermissionsAsync(string? systemRole, Guid? userId, CancellationToken ct = default)
    {
        if (!IsAdmin())
        {
            return Result.Forbidden<List<SystemModulePermissionDto>>();
        }

        var query = _systemPermRepo.GetQueryable();
        if (!string.IsNullOrEmpty(systemRole))
        {
            query = query.Where(p => p.SystemRole == systemRole);
        }
        else if (userId.HasValue)
        {
            query = query.Where(p => p.UserId == userId.Value);
        }

        var perms = await query.ToListAsync(ct);
        var dtos = perms.Select(p => new SystemModulePermissionDto(
            p.Id, p.SystemRole, p.UserId, p.ModuleKey, p.IsAllowed, p.AiTier, p.CreatedAt
        )).ToList();

        return Result.Success(dtos);
    }

    public async Task<Result<SystemModulePermissionDto>> UpdateSystemModulePermissionAsync(string? systemRole, Guid? userId, UpdateSystemModulePermissionDto dto, CancellationToken ct = default)
    {
        if (!IsAdmin()) return Result.Forbidden<SystemModulePermissionDto>();

        var existing = await _systemPermRepo.GetQueryable()
            .FirstOrDefaultAsync(p => (systemRole != null && p.SystemRole == systemRole && p.ModuleKey == dto.ModuleKey) ||
                                       (userId != null && p.UserId == userId && p.ModuleKey == dto.ModuleKey), ct);

        if (existing == null)
        {
            existing = new SystemModulePermission
            {
                SystemRole = systemRole,
                UserId = userId,
                ModuleKey = dto.ModuleKey,
                IsAllowed = dto.IsAllowed,
                AiTier = dto.AiTier
            };
            await _systemPermRepo.AddAsync(existing, ct);
        }
        else
        {
            existing.IsAllowed = dto.IsAllowed;
            existing.AiTier = dto.AiTier;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _systemPermRepo.UpdateAsync(existing, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _auditLogService.LogAsync("UpdateSystemModulePermission", nameof(SystemModulePermission), existing.Id.ToString(), new { systemRole, userId, dto.ModuleKey, dto.IsAllowed }, ct);

        return Result.Success(new SystemModulePermissionDto(
            existing.Id, existing.SystemRole, existing.UserId, existing.ModuleKey, existing.IsAllowed, existing.AiTier, existing.CreatedAt
        ));
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin())
        {
            return true;
        }

        return await _projectRepo.GetQueryable().AnyAsync(
            project => project.Id == projectId
                && (project.OwnerId == currentUserId
                    || project.Members.Any(member => member.UserId == currentUserId)),
            ct);
    }

    private async Task<bool> CanManageProjectAsync(Guid projectId, CancellationToken ct)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return false;
        }

        if (IsAdmin())
        {
            return true;
        }

        var project = await _projectRepo.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId, ct);
        if (project == null)
        {
            return false;
        }

        if (project.OwnerId == currentUserId)
        {
            return true;
        }

        var role = await _memberRepo.GetQueryable()
            .Where(member => member.ProjectId == projectId && member.UserId == currentUserId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(ct);
        var resolvedRole = await _roleCatalog.ResolveAsync(role, project.OrganizationId, ct);
        return resolvedRole != null && ProjectRoleRules.CanManageProject(resolvedRole.BaseRole);
    }

    private bool IsAdmin() => ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);
}
