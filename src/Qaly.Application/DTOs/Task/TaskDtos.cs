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
    bool IsRestricted,
    bool IsPinned,
    bool ContributesToProgress,
    int UpvoteCount,
    int DownvoteCount,
    Guid ProjectId,
    string ProjectName,
    Guid? AssigneeId,
    string? AssigneeName,
    IReadOnlyList<TaskAssigneeDto> Assignees,
    IReadOnlyList<TaskLabelDto> Labels,
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
    bool IsPrivate = false,
    bool IsPinned = false,
    bool ContributesToProgress = true,
    IReadOnlyList<Guid>? AssigneeIds = null,
    IReadOnlyList<Guid>? LabelIds = null);

public record UpdateTaskDto(
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateTimeOffset? DueDate,
    int? EstimatedHours,
    int? ActualHours,
    Guid? AssigneeId,
    bool IsPrivate,
    bool IsPinned = false,
    bool ContributesToProgress = true,
    IReadOnlyList<Guid>? AssigneeIds = null,
    IReadOnlyList<Guid>? LabelIds = null);

public record TaskAssigneeDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl);

public record TaskLabelDto(
    Guid Id,
    string Name,
    string Color);
