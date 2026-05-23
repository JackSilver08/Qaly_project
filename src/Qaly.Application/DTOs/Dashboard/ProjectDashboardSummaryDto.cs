namespace Qaly.Application.DTOs.Dashboard;

public sealed record ProjectDashboardSummaryDto(
    DateTimeOffset GeneratedAt,
    Guid ProjectId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    ProjectDashboardSummaryMetricsDto Metrics,
    IReadOnlyList<ProjectDashboardStatusBreakdownDto> StatusBreakdown,
    ProjectDashboardDataQualityDto DataQuality);

public sealed record ProjectDashboardSummaryMetricsDto(
    int TotalTasks,
    int OpenTasks,
    int BacklogTasks,
    int InProgressTasks,
    int DoneTasks,
    int CancelledTasks,
    decimal CompletionRate,
    int OverdueTasks,
    int DueSoon24h);

public sealed record ProjectDashboardStatusBreakdownDto(
    string Status,
    int Count);

public sealed record ProjectDashboardDataQualityDto(
    int MissingDueDateOpen,
    int MissingAssigneeOpen);
