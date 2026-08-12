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
    DateTimeOffset? ArchivedAt = null,
    ProjectPermissionsDto? Permissions = null);

/// <summary>
/// What the calling user may do inside one project, resolved on the server.
///
/// The UI renders from this instead of re-deriving permissions from the role string, so the two
/// cannot drift. It is a rendering aid, not an authorization control: every endpoint still checks
/// permissions independently.
/// </summary>
public record ProjectPermissionsDto(
    string Role,
    string RoleLabel,
    bool CanManageProject,
    bool CanManageMembers,
    bool CanManageAllTasks,
    bool CanCreateTask,
    bool CanUpdateOwnTasks,
    bool CanComment,
    bool CanTrackTime,
    bool CanReviewEvidence,
    bool CanReadInternalWiki,
    bool CanWriteWiki,
    bool CanManageIntegrations,
    string AiTier,
    string AiTierDescription);

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
    string? Description,
    Guid? OwnerId = null);

public record UpdateOrganizationDto(
    string Name,
    string? Code,
    string? Description,
    bool IsActive = true,
    string? AllowedEmailDomains = null,
    string? WorkspaceIcon = null,
    string? WorkspaceCover = null,
    Guid? OwnerId = null);

public record OrganizationMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateTimeOffset JoinedAt);
