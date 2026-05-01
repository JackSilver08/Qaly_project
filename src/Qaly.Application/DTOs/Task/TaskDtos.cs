namespace Qaly.Application.DTOs.Task;

public record TaskItemDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateTimeOffset? DueDate,
    int? EstimatedHours,
    int? ActualHours,
    bool IsPrivate,
    Guid ProjectId,
    string ProjectName,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid ReporterId,
    string ReporterName,
    int CommentCount,
    int AttachmentCount,
    string? AiPrioritySuggestion,
    DateTimeOffset CreatedAt);

public record CreateTaskDto(
    string Title,
    string? Description,
    string Priority,
    DateTimeOffset? DueDate,
    int? EstimatedHours,
    Guid ProjectId,
    Guid? AssigneeId,
    bool IsPrivate = false);

public record UpdateTaskDto(
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateTimeOffset? DueDate,
    int? EstimatedHours,
    int? ActualHours,
    Guid? AssigneeId,
    bool IsPrivate);
