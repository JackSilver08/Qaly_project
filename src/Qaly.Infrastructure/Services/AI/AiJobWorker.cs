using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;

namespace Qaly.Infrastructure.Services.AI;

public sealed partial class AiJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<AiJobPlatformOptions> _options;
    private readonly ILogger<AiJobWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public AiJobWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AiJobPlatformOptions> options,
        ILogger<AiJobWorker> logger)
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

    private async Task<AiJobLease?> ClaimAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAiJobDispatchStore>();
        return await store.ClaimNextAsync(_workerId, LeaseDuration(), ct);
    }

    internal async Task ProcessLeaseAsync(AiJobLease lease, CancellationToken stoppingToken)
    {
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var stopHeartbeat = new CancellationTokenSource();
        var heartbeatTask = HeartbeatAsync(
            lease.DispatchId,
            processing,
            stopHeartbeat.Token);
        Exception? processingFailure = null;
        var hostCanceled = false;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IAiJobProcessor>();
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
            await TryReleaseLeaseAsync(lease.DispatchId);
            throw new OperationCanceledException(stoppingToken);
        }

        if (!heartbeat.LeaseRetained)
        {
            LeaseOwnershipLost(_logger, lease.JobId, heartbeat.Error);
            return;
        }

        if (processingFailure != null)
        {
            JobProcessingFailed(_logger, lease.JobId, processingFailure);
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IAiJobDispatchStore>();
            await store.AbandonLeaseAsync(
                lease.DispatchId,
                _workerId,
                AiErrorCodes.ProviderUnavailable,
                processingFailure.Message,
                stoppingToken);
        }
    }

    private async Task<HeartbeatResult> HeartbeatAsync(
        Guid dispatchId,
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
                var store = scope.ServiceProvider.GetRequiredService<IAiJobDispatchStore>();
                if (await store.RenewLeaseAsync(
                        dispatchId,
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

    private async Task TryReleaseLeaseAsync(Guid dispatchId)
    {
        try
        {
            using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IAiJobDispatchStore>();
            await store.ReleaseLeaseAsync(dispatchId, _workerId, recoveryTimeout.Token);
        }
        catch (Exception exception)
        {
            LeaseReleaseFailed(_logger, dispatchId, exception);
        }
    }

    private TimeSpan LeaseDuration()
        => TimeSpan.FromSeconds(Math.Max(10, _options.CurrentValue.LeaseSeconds));

    private Task DelayAsync(CancellationToken ct)
        => Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _options.CurrentValue.PollIntervalMilliseconds)), ct);

    [LoggerMessage(Level = LogLevel.Information, Message = "AI job worker {WorkerId} started.")]
    private static partial void WorkerStarted(ILogger logger, string workerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "AI job worker {WorkerId} stopped.")]
    private static partial void WorkerStopped(ILogger logger, string workerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "AI job worker loop failed.")]
    private static partial void WorkerLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "AI job {JobId} processing failed unexpectedly.")]
    private static partial void JobProcessingFailed(ILogger logger, Guid jobId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI job {JobId} stopped because its durable lease could not be renewed or was lost.")]
    private static partial void LeaseOwnershipLost(ILogger logger, Guid jobId, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not release canceled AI job dispatch {DispatchId}; lease-expiry recovery may be required.")]
    private static partial void LeaseReleaseFailed(ILogger logger, Guid dispatchId, Exception exception);

    private sealed record HeartbeatResult(bool LeaseRetained, Exception? Error);
}
