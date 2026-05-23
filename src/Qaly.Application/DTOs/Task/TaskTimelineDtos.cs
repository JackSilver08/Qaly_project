namespace Qaly.Application.DTOs.Task;

public record ProjectTimelineDto(
    Guid ProjectId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset SprintStart,
    DateTimeOffset SprintEnd,
    int TotalTasks,
    int OpenTasks,
    int DoneTasks,
    int OverdueTasks,
    int BlockedTasks,
    IReadOnlyList<SprintBucketDto> Buckets,
    IReadOnlyList<TimelineDependencyDto> BlockedItems);

public record SprintBucketDto(
    string Label,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int TaskCount,
    int DoneCount,
    int OverdueCount,
    int ActiveCount,
    int PlannedPoints);

public record TimelineDependencyDto(
    Guid TaskId,
    string Title,
    string Status,
    DateTimeOffset? DueDate,
    IReadOnlyList<Guid> BlockingTaskIds,
    bool IsBlocked);

