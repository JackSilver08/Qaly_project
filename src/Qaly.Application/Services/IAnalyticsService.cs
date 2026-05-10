using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Analytics;

namespace Qaly.Application.Services;

public interface IAnalyticsService
{
    Task<Result<ProjectAnalyticsDto>> GetProjectAnalyticsAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<WorkspaceAnalyticsDto>> GetWorkspaceAnalyticsAsync(CancellationToken ct = default);
}
