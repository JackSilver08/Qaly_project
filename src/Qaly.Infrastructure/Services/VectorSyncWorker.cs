using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Services;

public sealed partial class VectorSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<VectorSyncOptions> _options;
    private readonly ILogger<VectorSyncWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public VectorSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<VectorSyncOptions> options,
        ILogger<VectorSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        VectorSyncWorkerStarting(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0) await DelayAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ErrorProcessingVectorSyncOutbox(_logger, exception);
                await DelayAsync(stoppingToken);
            }
        }

        VectorSyncWorkerStopping(_logger);
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        var options = _options.CurrentValue;
        var leaseDuration = TimeSpan.FromSeconds(Math.Clamp(options.LeaseSeconds, 30, 900));
        var heartbeatInterval = TimeSpan.FromSeconds(Math.Clamp(
            options.HeartbeatSeconds,
            5,
            Math.Max(5, (int)leaseDuration.TotalSeconds / 2)));
        var baseRetryDelay = TimeSpan.FromSeconds(Math.Clamp(options.BaseRetrySeconds, 1, 300));
        var processed = 0;

        for (var index = 0; index < Math.Clamp(options.BatchSize, 1, 100); index++)
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IVectorSyncOutboxStore>();
            var ingestion = scope.ServiceProvider.GetRequiredService<IAiIngestionService>();
            var item = await store.ClaimNextAsync(
                _workerId,
                leaseDuration,
                Math.Clamp(options.MaxAttempts, 1, 20),
                ct);
            if (item == null) break;

            await ProcessClaimAsync(
                item,
                store,
                ingestion,
                leaseDuration,
                heartbeatInterval,
                baseRetryDelay,
                Math.Clamp(options.MaxAttempts, 1, 20),
                ct);
            processed++;
        }

        return processed;
    }

    private async Task ProcessClaimAsync(
        VectorSyncOutbox item,
        IVectorSyncOutboxStore store,
        IAiIngestionService ingestion,
        TimeSpan leaseDuration,
        TimeSpan heartbeatInterval,
        TimeSpan baseRetryDelay,
        int maxAttempts,
        CancellationToken ct)
    {
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var stopHeartbeat = new CancellationTokenSource();
        var heartbeatTask = MaintainLeaseAsync(
            item.Id,
            leaseDuration,
            heartbeatInterval,
            processing,
            stopHeartbeat.Token);
        Exception? processingFailure = null;
        var hostCanceled = false;

        try
        {
            await ProcessMessageAsync(item, ingestion, processing.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            hostCanceled = true;
        }
        catch (OperationCanceledException) when (processing.IsCancellationRequested)
        {
            // Lease heartbeat canceled processing because ownership is uncertain.
        }
        catch (Exception exception)
        {
            processingFailure = exception;
        }

        stopHeartbeat.Cancel();
        var heartbeat = await heartbeatTask;

        if (hostCanceled)
        {
            await TryReleaseClaimAsync(item.Id);
            throw new OperationCanceledException(ct);
        }

        if (!heartbeat.LeaseRetained)
        {
            LeaseOwnershipLost(_logger, item.Id, heartbeat.Error);
            return;
        }

        if (processingFailure != null)
        {
            await store.RecordFailureAsync(
                item,
                _workerId,
                processingFailure.Message,
                maxAttempts,
                baseRetryDelay,
                ct);
            FailedToProcessOutboxMessage(
                _logger,
                item.Id,
                item.RetryCount,
                item.DeadLetteredAt != null,
                processingFailure);
            return;
        }

        await store.CompleteAsync(item, _workerId, ct);
    }

    private static Task ProcessMessageAsync(
        VectorSyncOutbox item,
        IAiIngestionService ingestion,
        CancellationToken ct)
        => item.EventType switch
        {
            VectorSyncEventTypes.TaskCreated or VectorSyncEventTypes.TaskUpdated
                => ingestion.SyncTaskAsync(item.AggregateId, ct),
            VectorSyncEventTypes.TaskDeleted
                => ingestion.DeleteTaskAsync(item.AggregateId, ct),
            VectorSyncEventTypes.CommentAdded or VectorSyncEventTypes.CommentUpdated
                => ingestion.SyncCommentAsync(item.AggregateId, ct),
            VectorSyncEventTypes.CommentDeleted
                => ingestion.DeleteCommentAsync(item.AggregateId, ct),
            VectorSyncEventTypes.TaskAttachmentCreated or VectorSyncEventTypes.TaskAttachmentUpdated
                => ingestion.SyncAttachmentAsync(item.AggregateId, ct),
            VectorSyncEventTypes.TaskAttachmentDeleted
                => ingestion.DeleteAttachmentAsync(item.AggregateId, ct),
            VectorSyncEventTypes.ProjectCreated or VectorSyncEventTypes.ProjectUpdated
                => ingestion.SyncProjectAsync(item.AggregateId, ct),
            VectorSyncEventTypes.ProjectDeleted
                => ingestion.DeleteProjectAsync(item.AggregateId, ct),
            _ => throw new InvalidOperationException($"Unsupported vector sync event '{item.EventType}'.")
        };

    private async Task<HeartbeatResult> MaintainLeaseAsync(
        Guid outboxId,
        TimeSpan leaseDuration,
        TimeSpan heartbeatInterval,
        CancellationTokenSource processing,
        CancellationToken stopToken)
    {
        using var timer = new PeriodicTimer(heartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stopToken))
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IVectorSyncOutboxStore>();
                if (await store.RenewLeaseAsync(outboxId, _workerId, leaseDuration, stopToken)) continue;

                processing.Cancel();
                return new HeartbeatResult(false, null);
            }
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
            return new HeartbeatResult(true, null);
        }
        catch (Exception exception)
        {
            processing.Cancel();
            return new HeartbeatResult(false, exception);
        }

        return new HeartbeatResult(true, null);
    }

    private async Task TryReleaseClaimAsync(Guid outboxId)
    {
        try
        {
            using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IVectorSyncOutboxStore>();
            await store.ReleaseLeaseAsync(outboxId, _workerId, recoveryTimeout.Token);
        }
        catch (Exception exception)
        {
            ClaimReleaseFailed(_logger, outboxId, exception);
        }
    }

    private Task DelayAsync(CancellationToken ct)
        => Task.Delay(
            TimeSpan.FromMilliseconds(Math.Clamp(
                _options.CurrentValue.PollIntervalMilliseconds,
                100,
                60000)),
            ct);

    [LoggerMessage(Level = LogLevel.Information, Message = "Vector sync worker is starting.")]
    private static partial void VectorSyncWorkerStarting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Vector sync worker loop failed.")]
    private static partial void ErrorProcessingVectorSyncOutbox(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Vector sync worker is stopping.")]
    private static partial void VectorSyncWorkerStopping(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Vector sync outbox {OutboxId} failed on attempt {Attempt}; dead-lettered: {DeadLettered}.")]
    private static partial void FailedToProcessOutboxMessage(
        ILogger logger,
        Guid outboxId,
        int attempt,
        bool deadLettered,
        Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Vector sync outbox {OutboxId} stopped because its durable lease could not be renewed or was lost.")]
    private static partial void LeaseOwnershipLost(ILogger logger, Guid outboxId, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not release canceled vector sync claim {OutboxId}; operator recovery may be required.")]
    private static partial void ClaimReleaseFailed(ILogger logger, Guid outboxId, Exception exception);

    private sealed record HeartbeatResult(bool LeaseRetained, Exception? Error);
}
