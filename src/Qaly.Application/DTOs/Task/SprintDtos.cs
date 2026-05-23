namespace Qaly.Application.DTOs.Task;

public record SprintDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string Status,
    string? Goal,
    int TaskCount,
    int CompletedTaskCount,
    int Progress);

public record CreateSprintRequest(
    string Name,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Goal);

public record UpdateSprintRequest(
    string Name,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string Status,
    string? Goal);
