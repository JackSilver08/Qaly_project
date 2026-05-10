namespace Qaly.Application.DTOs.Analytics;

public record ProjectAnalyticsDto(
    int TotalTasks,
    int DoneTasks,
    int InProgressTasks,
    int OverdueTasks,
    double TotalEstimatedHours,
    double TotalActualHours,
    List<MemberProductivityDto> MemberProductivity,
    List<DailyProductivityDto> DailyProductivity
);

public record MemberProductivityDto(
    Guid UserId,
    string FullName,
    int AssignedTasks,
    int DoneTasks,
    double LoggedHours
);

public record DailyProductivityDto(
    DateTimeOffset Date,
    int CompletedTasks,
    double LoggedHours
);

public record WorkspaceAnalyticsDto(
    int TotalProjects,
    int ActiveProjects,
    int TotalTasks,
    int DoneTasksThisWeek,
    double TotalHoursLoggedThisWeek
);
