namespace Qaly.Application.Common.Interfaces;

/// <summary>
/// Dashboard service - tổng hợp dữ liệu cho trang dashboard.
/// AI sẽ enhance thêm insights.
/// </summary>
public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken ct = default);
}

public record DashboardDto(
    int TotalProjects,
    int TotalTasks,
    int TasksTodo,
    int TasksInProgress,
    int TasksDone,
    int TasksOverdue,
    IReadOnlyList<RecentActivityDto> RecentActivities,
    string? AiInsight);

public record RecentActivityDto(
    string Action,
    string EntityType,
    string EntityName,
    string UserName,
    DateTimeOffset Timestamp);
