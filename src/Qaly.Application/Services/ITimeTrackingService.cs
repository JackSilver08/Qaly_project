using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;

namespace Qaly.Application.Services;

public interface ITimeTrackingService
{
    Task<Result<TimeEntryDto>> StartTimerAsync(Guid taskId, CancellationToken ct = default);
    Task<Result<TimeEntryDto>> StopTimerAsync(Guid entryId, CancellationToken ct = default);
    Task<Result<TimeEntryDto>> AddManualEntryAsync(CreateTimeEntryDto dto, CancellationToken ct = default);
    Task<Result<List<TimeEntryDto>>> GetByTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<Result<List<TimeEntryDto>>> GetByProjectAsync(Guid projectId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default);
}
