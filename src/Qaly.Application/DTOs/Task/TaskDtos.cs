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
    DateTimeOffset CreatedAt,
    int SortOrder,
    string RowVersion,
    int Number = 0,
    string? Key = null,
    Guid? ParentTaskId = null,
    int SubtaskCount = 0,
    int CompletedSubtaskCount = 0,
    int SubtaskProgressPercentage = 0,
    Guid? ReviewerId = null,
    string? ReviewerName = null,
    Guid? SprintId = null);

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
    IReadOnlyList<Guid>? LabelIds = null,
    Guid? SprintId = null);

public record SuggestTaskPriorityDto(
    Guid ProjectId,
    string Title,
    string? Description);

public record TaskPrioritySuggestionDto(
    string Priority,
    string Reason);

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
    IReadOnlyList<Guid>? LabelIds = null,
    string? RowVersion = null,
    Guid? SprintId = null);

public record TaskAssigneeDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl);

public record TaskLabelDto(
    Guid Id,
    string Name,
    string Color);

public record TaskAttentionDto(
    Guid Id,
    string Title,
    Guid ProjectId,
    string ProjectName,
    string Status,
    string Priority,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    Guid ReporterId,
    string ReporterName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTimeOffset AssignedAt,
    DateTimeOffset? LastViewedAt,
    bool IsDueSoon,
    bool IsOverdue,
    bool IsStaleTodo,
    bool IsStaleInProgress,
    bool IsUnseenByAssignee,
    IEnumerable<string> AttentionReasons,
    IEnumerable<string> AllowedActions);

public record TaskDependencyDto(
    Guid Id,
    Guid PredecessorId,
    string PredecessorTitle,
    Guid SuccessorId,
    string SuccessorTitle,
    string Type);


public record KanbanBoardDto(
    Guid ProjectId,
    IReadOnlyList<KanbanColumnDto> Columns);

public record KanbanColumnDto(
    string Status,
    IReadOnlyList<TaskItemDto> Tasks);

public record KanbanMoveRequest(
    Guid TaskId,
    string FromStatus,
    string ToStatus,
    Guid? BeforeTaskId,
    Guid? AfterTaskId,
    string? RowVersion);

public record KanbanMoveResultDto(
    TaskItemDto Task,
    KanbanBoardDto Board);
