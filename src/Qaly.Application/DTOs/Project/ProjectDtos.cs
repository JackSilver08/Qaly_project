namespace Qaly.Application.DTOs.Project;

public record ProjectDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string? LogoUrl,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    Guid OwnerId,
    string OwnerName,
    int MemberCount,
    int TaskCount,
    int ProgressPercentage,
    IReadOnlyList<ProjectLabelDto> Labels,
    DateTimeOffset CreatedAt,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? SourceGroupId = null,
    bool EnableOnHold = true,
    bool EnableInReview = true,
    bool RequireEvidenceToDone = false,
    bool RestrictTransitionsToAdmin = false,
    DateTimeOffset? DeletedAt = null,
    DateTimeOffset? ArchivedAt = null);

public record CreateProjectDto(
    string Name,
    string? Code,
    string? Description,
    string? LogoUrl,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    Guid? OrganizationId = null,
    Guid? SourceGroupId = null);

public record UpdateProjectDto(
    string Name,
    string? Code,
    string? Description,
    string? LogoUrl,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    Guid? OrganizationId = null,
    bool? EnableOnHold = null,
    bool? EnableInReview = null,
    bool? RequireEvidenceToDone = null,
    bool? RestrictTransitionsToAdmin = null);

public record ProjectLabelDto(
    Guid Id,
    string Name,
    string Color,
    DateTimeOffset CreatedAt);

public record CreateProjectLabelDto(
    string Name,
    string Color);

public record UpdateProjectLabelDto(
    string Name,
    string Color);

public record UpdateProjectMemberPermissionsDto(
    bool CanViewProjectTimeline,
    bool CanViewTaskRisk,
    bool CanNudgeAssignee,
    bool CanViewUnseenTaskSignal);

public record OrganizationDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    Guid OwnerId,
    string OwnerName,
    int MemberCount,
    int ProjectCount,
    DateTimeOffset CreatedAt,
    string? AllowedEmailDomains = null,
    string? WorkspaceIcon = null,
    string? WorkspaceCover = null);

public record CreateOrganizationDto(
    string Name,
    string? Code,
    string? Description);

public record UpdateOrganizationDto(
    string Name,
    string? Code,
    string? Description,
    bool IsActive = true,
    string? AllowedEmailDomains = null,
    string? WorkspaceIcon = null,
    string? WorkspaceCover = null);

public record OrganizationMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateTimeOffset JoinedAt);
