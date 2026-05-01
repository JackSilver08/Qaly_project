namespace Qaly.Application.DTOs.Project;

public record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    Guid OwnerId,
    string OwnerName,
    int MemberCount,
    int TaskCount,
    DateTimeOffset CreatedAt);

public record CreateProjectDto(
    string Name,
    string? Description,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate);

public record UpdateProjectDto(
    string Name,
    string? Description,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate);
