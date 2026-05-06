namespace Qaly.Application.DTOs.Task;

public record TimeEntryDto(
    Guid Id,
    Guid TaskId,
    string TaskTitle,
    Guid UserId,
    string UserName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int? ManualMinutes,
    int TotalMinutes,
    string? Note,
    DateTimeOffset CreatedAt);

public record CreateTimeEntryDto(
    Guid TaskId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt = null,
    int? ManualMinutes = null,
    string? Note = null);
