using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Project;

namespace Qaly.Application.Services;

public interface IProjectRoleService
{
    Task<Result<List<ProjectCustomRoleDto>>> GetCustomRolesAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<ProjectCustomRoleDto>> CreateCustomRoleAsync(Guid projectId, CreateProjectCustomRoleDto dto, CancellationToken ct = default);
    Task<Result<RoleAssignConflictCheckResultDto>> CheckRoleAssignConflictsAsync(Guid projectId, Guid memberId, AssignProjectMemberRoleDto dto, CancellationToken ct = default);
    Task<Result<ProjectMemberRoleHistoryDto>> AssignRoleAsync(Guid projectId, Guid memberId, AssignProjectMemberRoleDto dto, CancellationToken ct = default);
    Task<Result<List<ProjectMemberRoleHistoryDto>>> GetMemberRoleHistoryAsync(Guid projectId, Guid memberId, CancellationToken ct = default);
    Task<Result<List<SystemModulePermissionDto>>> GetSystemModulePermissionsAsync(string? systemRole, Guid? userId, CancellationToken ct = default);
    Task<Result<SystemModulePermissionDto>> UpdateSystemModulePermissionAsync(string? systemRole, Guid? userId, UpdateSystemModulePermissionDto dto, CancellationToken ct = default);
}
