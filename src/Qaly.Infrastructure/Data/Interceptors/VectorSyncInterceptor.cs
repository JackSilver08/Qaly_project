using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Qaly.Domain.Entities;
using System.Text.Json;

namespace Qaly.Infrastructure.Data.Interceptors;

public class VectorSyncInterceptor : SaveChangesInterceptor
{
    private readonly bool _enabled;

    public VectorSyncInterceptor(IConfiguration configuration)
    {
        _enabled = configuration.GetValue<bool>("Ai:SemanticEnabled");
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (!_enabled || context == null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var contract = ResolveContract(entry.Entity, entry.State);
            if (contract == null) continue;

            var outboxMessage = new VectorSyncOutbox
            {
                EventType = contract.Value.EventType,
                AggregateType = contract.Value.AggregateType,
                AggregateId = entry.Entity.Id,
                Payload = JsonSerializer.Serialize(new { Id = entry.Entity.Id })
            };

            context.Set<VectorSyncOutbox>().Add(outboxMessage);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    internal static (string EventType, string AggregateType)? ResolveContract(
        BaseEntity entity,
        EntityState state)
        => (entity, state) switch
        {
            (Project, EntityState.Added) => (VectorSyncEventTypes.ProjectCreated, VectorSyncAggregateTypes.Project),
            (Project, EntityState.Modified) => (VectorSyncEventTypes.ProjectUpdated, VectorSyncAggregateTypes.Project),
            (Project, EntityState.Deleted) => (VectorSyncEventTypes.ProjectDeleted, VectorSyncAggregateTypes.Project),
            (TaskItem, EntityState.Added) => (VectorSyncEventTypes.TaskCreated, VectorSyncAggregateTypes.Task),
            (TaskItem, EntityState.Modified) => (VectorSyncEventTypes.TaskUpdated, VectorSyncAggregateTypes.Task),
            (TaskItem, EntityState.Deleted) => (VectorSyncEventTypes.TaskDeleted, VectorSyncAggregateTypes.Task),
            (TaskComment, EntityState.Added) => (VectorSyncEventTypes.CommentAdded, VectorSyncAggregateTypes.Comment),
            (TaskComment, EntityState.Modified) => (VectorSyncEventTypes.CommentUpdated, VectorSyncAggregateTypes.Comment),
            (TaskComment, EntityState.Deleted) => (VectorSyncEventTypes.CommentDeleted, VectorSyncAggregateTypes.Comment),
            (TaskAttachment, EntityState.Added) => (VectorSyncEventTypes.TaskAttachmentCreated, VectorSyncAggregateTypes.Attachment),
            (TaskAttachment, EntityState.Modified) => (VectorSyncEventTypes.TaskAttachmentUpdated, VectorSyncAggregateTypes.Attachment),
            (TaskAttachment, EntityState.Deleted) => (VectorSyncEventTypes.TaskAttachmentDeleted, VectorSyncAggregateTypes.Attachment),
            _ => null
        };
}
