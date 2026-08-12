namespace Qaly.Application.DTOs.Project;

public record ProjectCustomRoleDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    string? ColorCode,
    bool IsSystemDefault,
    string PermissionMatrixJson,
    DateTimeOffset CreatedAt);

public record CreateProjectCustomRoleDto(
    string Name,
    string? Description,
    string? ColorCode,
    string? PermissionMatrixJson);

public record ProjectMemberRoleHistoryDto(
    Guid Id,
    Guid ProjectMemberId,
    Guid RoleId,
    string RoleName,
    string? RoleColorCode,
    string? PhaseName,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    bool IsActive,
    string? ReasonOrNote,
    Guid AssignedByUserId,
    string AssignedByUserName);

public record AssignProjectMemberRoleDto(
    Guid RoleId,
    string? PhaseName,
    DateTimeOffset StartDate,
    string? ReasonOrNote,
    bool ForceOverlapResolution = false,
    bool ForceSystemConflictResolution = false);

public record RoleAssignConflictCheckResultDto(
    bool HasOverlapConflict,
    string? ActiveRoleName,
    DateTimeOffset? ActiveRoleStartDate,
    bool HasSystemRoleConflict,
    string? SystemRole,
    string? SystemRoleAiTier,
    string? Message);

public record SystemModulePermissionDto(
    Guid Id,
    string? SystemRole,
    Guid? UserId,
    string ModuleKey,
    bool IsAllowed,
    string AiTier,
    DateTimeOffset CreatedAt);

public record UpdateSystemModulePermissionDto(
    string ModuleKey,
    bool IsAllowed,
    string AiTier = "Full");
