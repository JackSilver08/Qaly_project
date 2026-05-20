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
    string? OrganizationName);

public record CreateProjectDto(
    string Name,
    string? Code,
    string? Description,
    string? LogoUrl,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    Guid? OrganizationId = null);

public record UpdateProjectDto(
    string Name,
    string? Code,
    string? Description,
    string? LogoUrl,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    Guid? OrganizationId = null);

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
    DateTimeOffset CreatedAt);

public record CreateOrganizationDto(
    string Name,
    string? Code,
    string? Description);

public record UpdateOrganizationDto(
    string Name,
    string? Code,
    string? Description,
    bool IsActive = true);

public record OrganizationMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateTimeOffset JoinedAt);
