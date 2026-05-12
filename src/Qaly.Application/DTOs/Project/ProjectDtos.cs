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
    DateTimeOffset CreatedAt);

public record CreateProjectDto(
    string Name,
    string? Code,
    string? Description,
    string? LogoUrl,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate);

public record UpdateProjectDto(
    string Name,
    string? Code,
    string? Description,
    string? LogoUrl,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate);

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
