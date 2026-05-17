using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;

namespace Qaly.Application.Services;

public interface ITaskService
{
    Task<Result<TaskItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<TaskItemDto>>> GetByProjectAsync(Guid projectId, string? status = null, string? priority = null, int page = 1, int pageSize = 20, string? search = null, Guid? assigneeId = null, Guid? labelId = null, string sort = "default", CancellationToken ct = default);
    Task<Result<PagedResult<TaskItemDto>>> GetByAssigneeAsync(Guid assigneeId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<Result<PagedResult<TaskAttentionDto>>> GetAttentionByProjectAsync(Guid projectId, Guid? assigneeId = null, Guid? reporterId = null, string? status = null, string? priority = null, string? riskType = null, DateTimeOffset? from = null, DateTimeOffset? toDate = null, int page = 1, int pageSize = 25, string sort = "risk", CancellationToken ct = default);
    Task<Result<TaskItemDto>> CreateAsync(CreateTaskDto dto, CancellationToken ct = default);
    Task<Result<TaskItemDto>> UpdateAsync(Guid id, UpdateTaskDto dto, CancellationToken ct = default);
    Task<Result> UpdateStatusAsync(Guid id, string newStatus, CancellationToken ct = default);
    Task<Result> UpdateSortOrderAsync(Guid id, int sortOrder, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result> BatchDeleteAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<Result> BatchUpdateStatusAsync(IEnumerable<Guid> ids, string newStatus, CancellationToken ct = default);
    
    // Gantt Chart
    Task<Result<IEnumerable<GanttTaskDto>>> GetGanttDataAsync(Guid projectId, CancellationToken ct = default);
    Task<Result> UpdateDatesAsync(Guid taskId, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct = default);
    Task<Result> MarkViewedAsync(Guid projectId, Guid taskId, CancellationToken ct = default);
    Task<Result> NudgeAssigneeAsync(Guid projectId, Guid taskId, Guid? assigneeId = null, CancellationToken ct = default);
    Task<Result> AddDependencyAsync(Guid predecessorId, Guid successorId, string type = "FinishToStart", CancellationToken ct = default);
    Task<Result> RemoveDependencyAsync(Guid dependencyId, CancellationToken ct = default);
}
