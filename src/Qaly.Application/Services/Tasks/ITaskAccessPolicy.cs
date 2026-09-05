using Qaly.Domain.Entities;

namespace Qaly.Application.Services.Tasks;

public interface ITaskAccessPolicy
{
    Guid? CurrentUserId { get; }
    bool IsAdmin { get; }
    IQueryable<TaskItem> ApplyVisibilityFilter(IQueryable<TaskItem> query);
    Task<bool> CanAccessTaskAsync(TaskItem task, CancellationToken ct);
    Task<bool> CanContributeToTaskAsync(TaskItem task, CancellationToken ct);
    Task<bool> CanManageTaskAsync(TaskItem task, CancellationToken ct);
    Task<bool> CanReviewTaskAsync(TaskItem task, CancellationToken ct);
    Task<bool> CanAccessProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanContributeToProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanCreateTaskAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanManageProjectAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanViewProjectTimelineAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanViewTaskRiskAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanViewUnseenTaskSignalAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanNudgeAssigneeAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanManageWebhooksAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanViewProjectWorkloadAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanReadWikiAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanReadInternalWikiAsync(Guid projectId, Guid ownerId, CancellationToken ct);
    Task<bool> CanWriteWikiAsync(Guid projectId, Guid ownerId, CancellationToken ct);
}
