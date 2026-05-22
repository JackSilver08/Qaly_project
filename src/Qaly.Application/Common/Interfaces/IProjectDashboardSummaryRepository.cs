using Qaly.Domain.Entities;

namespace Qaly.Application.Common.Interfaces;

public interface IProjectDashboardSummaryRepository
{
    Task<ProjectDashboardProjectInfo?> GetProjectInfoAsync(Guid projectId, CancellationToken ct = default);
    IQueryable<TaskItem> QueryProjectTasks(Guid projectId, DateTimeOffset? from, DateTimeOffset? toDate);
}

public sealed record ProjectDashboardProjectInfo(Guid ProjectId, Guid OwnerId);
