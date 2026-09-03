using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;

namespace Qaly.Infrastructure.Services.Privacy;

public sealed partial class PrivacyWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<PrivacyV4Options> _options;
    private readonly ILogger<PrivacyWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:privacy:{Guid.NewGuid():N}";

    public PrivacyWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<PrivacyV4Options> options,
        ILogger<PrivacyWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerStarted(_logger, _workerId);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_options.CurrentValue.Enabled || !_options.CurrentValue.WorkerEnabled)
                {
                    await DelayAsync(stoppingToken);
                    continue;
                }

                var processed = 0;
                var batchSize = Math.Clamp(_options.CurrentValue.BatchSize, 1, 100);
                while (processed < batchSize && !stoppingToken.IsCancellationRequested)
                {
                    var lease = await ClaimAsync(stoppingToken);
                    if (lease == null)
                    {
                        break;
                    }

                    await ProcessLeaseAsync(lease, stoppingToken);
                    processed++;
                }

                if (processed == 0)
                {
                    await DelayAsync(stoppingToken);
                }
                else
                {
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                WorkerLoopFailed(_logger, ex);
                await DelayAsync(stoppingToken);
            }
        }

        WorkerStopped(_logger, _workerId);
    }

    private async Task<PrivacyWorkLease?> ClaimAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPrivacyWorkStore>();
        return await store.ClaimNextAsync(_workerId, LeaseDuration(), ct);
    }

    internal async Task ProcessLeaseAsync(PrivacyWorkLease lease, CancellationToken stoppingToken)
    {
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var stopHeartbeat = new CancellationTokenSource();
        var heartbeatTask = HeartbeatAsync(
            lease,
            processing,
            stopHeartbeat.Token);
        Exception? processingFailure = null;
        var hostCanceled = false;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPrivacyWorkProcessor>();
            await processor.ProcessAsync(lease, _workerId, processing.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            hostCanceled = true;
        }
        catch (OperationCanceledException) when (processing.IsCancellationRequested)
        {
            // Lease heartbeat canceled processing because ownership is uncertain.
        }
        catch (Exception ex)
        {
            processingFailure = ex;
        }

        stopHeartbeat.Cancel();
        var heartbeat = await heartbeatTask;

        if (hostCanceled)
        {
            await TryReleaseLeaseAsync(lease);
            throw new OperationCanceledException(stoppingToken);
        }

        if (!heartbeat.LeaseRetained)
        {
            LeaseOwnershipLost(_logger, lease.Kind, lease.WorkId, heartbeat.Error);
            return;
        }

        if (processingFailure != null)
        {
            WorkProcessingFailed(_logger, lease.Kind, lease.WorkId, processingFailure);
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IPrivacyWorkStore>();
            await store.AbandonLeaseAsync(
                lease,
                _workerId,
                PrivacyErrorCodes.WorkerUnavailable,
                processingFailure.Message,
                stoppingToken);
        }
    }

    private async Task<HeartbeatResult> HeartbeatAsync(
        PrivacyWorkLease lease,
        CancellationTokenSource processing,
        CancellationToken stopToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.HeartbeatSeconds));
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stopToken))
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IPrivacyWorkStore>();
                if (await store.RenewLeaseAsync(
                        lease,
                        _workerId,
                        LeaseDuration(),
                        stopToken))
                {
                    continue;
                }

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

    private async Task TryReleaseLeaseAsync(PrivacyWorkLease lease)
    {
        try
        {
            using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IPrivacyWorkStore>();
            await store.ReleaseLeaseAsync(lease, _workerId, recoveryTimeout.Token);
        }
        catch (Exception exception)
        {
            LeaseReleaseFailed(_logger, lease.Kind, lease.WorkId, exception);
        }
    }

    private TimeSpan LeaseDuration()
        => TimeSpan.FromSeconds(Math.Max(10, _options.CurrentValue.LeaseSeconds));

    private Task DelayAsync(CancellationToken ct)
        => Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _options.CurrentValue.PollIntervalMilliseconds)), ct);

    [LoggerMessage(Level = LogLevel.Information, Message = "Privacy worker {WorkerId} started.")]
    private static partial void WorkerStarted(ILogger logger, string workerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Privacy worker {WorkerId} stopped.")]
    private static partial void WorkerStopped(ILogger logger, string workerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Privacy worker loop failed.")]
    private static partial void WorkerLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Privacy work {Kind}/{WorkId} failed.")]
    private static partial void WorkProcessingFailed(ILogger logger, string kind, Guid workId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Privacy work {Kind}/{WorkId} stopped because its durable lease could not be renewed or was lost.")]
    private static partial void LeaseOwnershipLost(ILogger logger, string kind, Guid workId, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not release canceled privacy work {Kind}/{WorkId}; lease-expiry recovery may be required.")]
    private static partial void LeaseReleaseFailed(ILogger logger, string kind, Guid workId, Exception exception);

    private sealed record HeartbeatResult(bool LeaseRetained, Exception? Error);
}
