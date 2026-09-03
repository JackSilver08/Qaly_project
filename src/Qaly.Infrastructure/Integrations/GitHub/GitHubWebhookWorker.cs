using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Integrations.GitHub;

public sealed partial class GitHubWebhookWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<GitHubIntegrationOptions> _options;
    private readonly ILogger<GitHubWebhookWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public GitHubWebhookWorker(IServiceScopeFactory scopeFactory, IOptionsMonitor<GitHubIntegrationOptions> options,
        ILogger<GitHubWebhookWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_options.CurrentValue.Enabled || !_options.CurrentValue.WorkerEnabled)
                {
                    await DelayAsync(stoppingToken);
                    continue;
                }

                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0) await DelayAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                WorkerLoopFailed(_logger, ex);
                await DelayAsync(stoppingToken);
            }
        }
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

        for (var index = 0; index < Math.Max(1, options.BatchSize); index++)
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IGitHubWebhookInboxStore>();
            var processor = scope.ServiceProvider.GetRequiredService<IGitHubWebhookProcessor>();
            var item = await store.ClaimNextAsync(
                _workerId,
                leaseDuration,
                Math.Max(1, options.MaxAttempts),
                ct);
            if (item == null) break;

            await ProcessClaimAsync(
                item,
                store,
                processor,
                leaseDuration,
                heartbeatInterval,
                baseRetryDelay,
                ct);
            processed++;
        }

        return processed;
    }

    private async Task ProcessClaimAsync(
        GitHubWebhookInbox item,
        IGitHubWebhookInboxStore store,
        IGitHubWebhookProcessor processor,
        TimeSpan leaseDuration,
        TimeSpan heartbeatInterval,
        TimeSpan baseRetryDelay,
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
            await processor.ProcessAsync(item, processing.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            hostCanceled = true;
        }
        catch (OperationCanceledException) when (processing.IsCancellationRequested)
        {
            // The heartbeat cancels processing when ownership cannot be proven.
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
            LeaseOwnershipLost(_logger, item.DeliveryId, heartbeat.Error);
            return;
        }

        if (processingFailure != null)
        {
            await store.RecordFailureAsync(
                item,
                _workerId,
                processingFailure.Message,
                baseRetryDelay,
                ct);
            DeliveryFailed(_logger, item.DeliveryId, item.AttemptCount, processingFailure);
            return;
        }

        await store.CompleteAsync(item, _workerId, ct);
    }

    private async Task<HeartbeatResult> MaintainLeaseAsync(
        Guid inboxId,
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
                var store = scope.ServiceProvider.GetRequiredService<IGitHubWebhookInboxStore>();
                if (await store.RenewLeaseAsync(inboxId, _workerId, leaseDuration, stopToken)) continue;

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

    private async Task TryReleaseClaimAsync(Guid inboxId)
    {
        try
        {
            using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IGitHubWebhookInboxStore>();
            await store.ReleaseLeaseAsync(inboxId, _workerId, recoveryTimeout.Token);
        }
        catch (Exception exception)
        {
            ClaimReleaseFailed(_logger, inboxId, exception);
        }
    }

    private Task DelayAsync(CancellationToken ct)
        => Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _options.CurrentValue.PollIntervalMilliseconds)), ct);

    [LoggerMessage(Level = LogLevel.Error, Message = "GitHub webhook worker loop failed.")]
    private static partial void WorkerLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub webhook delivery {DeliveryId} failed on attempt {Attempt}.")]
    private static partial void DeliveryFailed(ILogger logger, string deliveryId, int attempt, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not release canceled GitHub webhook inbox claim {InboxId}; operator recovery may be required.")]
    private static partial void ClaimReleaseFailed(ILogger logger, Guid inboxId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub webhook delivery {DeliveryId} stopped because its durable lease could not be renewed or was lost.")]
    private static partial void LeaseOwnershipLost(ILogger logger, string deliveryId, Exception? exception);

    private sealed record HeartbeatResult(bool LeaseRetained, Exception? Error);
}
