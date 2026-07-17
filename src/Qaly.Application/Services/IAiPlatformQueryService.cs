using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IAiPlatformQueryService
{
    Task<Result<AiPlatformHealthDto>> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<Result<AiUsageSnapshotDto>> GetUsageAsync(
        Guid? projectId,
        DateTimeOffset? rangeStart,
        DateTimeOffset? rangeEnd,
        CancellationToken cancellationToken = default);
    Task<Result<AiBudgetSnapshotDto>> GetBudgetAsync(Guid projectId, CancellationToken cancellationToken = default);
}
