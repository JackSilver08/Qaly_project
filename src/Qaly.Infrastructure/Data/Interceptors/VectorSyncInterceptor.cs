using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Interceptors;

public class VectorSyncInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public VectorSyncInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return result;

        var entries = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Select(e => new { e.Entity.Id, Type = e.Entity.GetType().Name })
            .ToList();

        if (entries.Any())
        {
            // Run sync in background to not block the main request
            _ = Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var ingestionService = scope.ServiceProvider.GetRequiredService<IAiIngestionService>();

                foreach (var entry in entries)
                {
                    try 
                    {
                        if (entry.Type == nameof(Project)) await ingestionService.SyncProjectAsync(entry.Id);
                        else if (entry.Type == nameof(TaskItem)) await ingestionService.SyncTaskAsync(entry.Id);
                        else if (entry.Type == nameof(TaskComment)) await ingestionService.SyncCommentAsync(entry.Id);
                    }
                    catch 
                    {
                        // Silent fail for background sync
                    }
                }
            }, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
