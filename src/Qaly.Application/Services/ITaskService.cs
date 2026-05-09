using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Task;

namespace Qaly.Application.Services;

public interface ITaskService
{
    Task<Result<TaskItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PagedResult<TaskItemDto>>> GetByProjectAsync(Guid projectId, string? status = null, string? priority = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<Result<PagedResult<TaskItemDto>>> GetByAssigneeAsync(Guid assigneeId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<Result<TaskItemDto>> CreateAsync(CreateTaskDto dto, CancellationToken ct = default);
    Task<Result<TaskItemDto>> UpdateAsync(Guid id, UpdateTaskDto dto, CancellationToken ct = default);
    Task<Result> UpdateStatusAsync(Guid id, string newStatus, CancellationToken ct = default);
    Task<Result> UpdateSortOrderAsync(Guid id, int sortOrder, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
