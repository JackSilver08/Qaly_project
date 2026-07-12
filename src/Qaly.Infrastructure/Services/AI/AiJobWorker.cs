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

                var lease = await ClaimAsync(stoppingToken);
                if (lease == null)
                {
                    await DelayAsync(stoppingToken);
                    continue;
                }

                await ProcessLeaseAsync(lease, stoppingToken);
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

    private async Task ProcessLeaseAsync(AiJobLease lease, CancellationToken stoppingToken)
    {
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = HeartbeatAsync(lease.DispatchId, heartbeatCts.Token);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IAiJobProcessor>();
            await processor.ProcessAsync(lease, _workerId, stoppingToken);
        }
        catch (Exception ex)
        {
            JobProcessingFailed(_logger, lease.JobId, ex);
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IAiJobDispatchStore>();
            await store.AbandonLeaseAsync(
                lease.DispatchId,
                _workerId,
                AiErrorCodes.ProviderUnavailable,
                ex.Message,
                stoppingToken);
        }
        finally
        {
            heartbeatCts.Cancel();
            try
            {
                await heartbeat;
            }
            catch (OperationCanceledException)
            {
                // Expected when processing completes before the next heartbeat.
            }
        }
    }

    private async Task HeartbeatAsync(Guid dispatchId, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.HeartbeatSeconds));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IAiJobDispatchStore>();
            if (!await store.RenewLeaseAsync(dispatchId, _workerId, LeaseDuration(), ct)) return;
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
}
