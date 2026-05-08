using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Text.Json;

namespace Qaly.Infrastructure.Services;

public partial class VectorSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VectorSyncWorker> _logger;

    public VectorSyncWorker(IServiceScopeFactory scopeFactory, ILogger<VectorSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        VectorSyncWorkerStarting(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                ErrorProcessingVectorSyncOutbox(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        VectorSyncWorkerStopping(_logger);
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IRepository<VectorSyncOutbox>>();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IAiIngestionService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var messages = await outboxRepo.GetQueryable()
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                await ProcessMessageAsync(message, ingestionService);
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                FailedToProcessOutboxMessage(_logger, ex, message.Id);
                message.RetryCount++;
                message.ErrorMessage = ex.Message;
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ProcessMessageAsync(VectorSyncOutbox message, IAiIngestionService ingestionService)
    {
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
        var id = payload.GetProperty("Id").GetGuid();

        switch (message.EventType)
        {
            case "TaskCreated":
            case "TaskUpdated":
                await ingestionService.SyncTaskAsync(id);
                break;
            case "TaskDeleted":
                await ingestionService.DeleteTaskAsync(id);
                break;
            case "CommentAdded":
                await ingestionService.SyncCommentAsync(id);
                break;
            case "CommentDeleted":
                await ingestionService.DeleteCommentAsync(id);
                break;
            case "ProjectCreated":
            case "ProjectUpdated":
                await ingestionService.SyncProjectAsync(id);
                break;
            case "ProjectDeleted":
                await ingestionService.DeleteProjectAsync(id);
                break;
            default:
                UnknownEventType(_logger, message.EventType);
                break;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Vector Sync Worker is starting.")]
    private static partial void VectorSyncWorkerStarting(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error occurred while processing vector sync outbox.")]
    private static partial void ErrorProcessingVectorSyncOutbox(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Vector Sync Worker is stopping.")]
    private static partial void VectorSyncWorkerStopping(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to process outbox message {MessageId}")]
    private static partial void FailedToProcessOutboxMessage(ILogger logger, Exception exception, Guid messageId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Unknown event type: {EventType}")]
    private static partial void UnknownEventType(ILogger logger, string eventType);
}
