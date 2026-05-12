using Qaly.Domain.Entities;

namespace Qaly.Application.Services.Tasks;

public interface ITaskAccessPolicy
{
    Guid? CurrentUserId { get; }
    bool IsAdmin { get; }
    IQueryable<TaskItem> ApplyVisibilityFilter(IQueryable<TaskItem> query);
    Task<bool> CanAccessTaskAsync(TaskItem task, CancellationToken ct);
    Task<bool> CanManageTaskAsync(TaskItem task, CancellationToken ct);
    Task<bool> CanAccessProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct);
}
