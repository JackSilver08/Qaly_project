using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Dashboard;

namespace Qaly.Application.Services;

public interface IDashboardSummaryService
{
    Task<Result<ProjectDashboardSummaryDto>> GetProjectSummaryAsync(
        Guid projectId,
        DateTimeOffset? from = null,
        DateTimeOffset? toDate = null,
        CancellationToken ct = default);
}
