using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Qaly.Domain.Entities;
using System.Text.Json;

namespace Qaly.Infrastructure.Data.Interceptors;

public class VectorSyncInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            if (entityName != nameof(Project) && entityName != nameof(TaskItem) && entityName != nameof(TaskComment))
                continue;

            var eventType = entry.State switch
            {
                EntityState.Added => $"{entityName}Created",
                EntityState.Modified => $"{entityName}Updated",
                EntityState.Deleted => $"{entityName}Deleted",
                _ => null
            };

            if (eventType == null) continue;

            var outboxMessage = new VectorSyncOutbox
            {
                EventType = eventType,
                Payload = JsonSerializer.Serialize(new { Id = entry.Entity.Id })
            };

            context.Set<VectorSyncOutbox>().Add(outboxMessage);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
