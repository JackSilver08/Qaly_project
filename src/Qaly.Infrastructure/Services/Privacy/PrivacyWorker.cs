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

    private async Task<PrivacyWorkLease?> ClaimAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPrivacyWorkStore>();
        return await store.ClaimNextAsync(_workerId, LeaseDuration(), ct);
    }

    private async Task ProcessLeaseAsync(PrivacyWorkLease lease, CancellationToken stoppingToken)
    {
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = HeartbeatAsync(lease, heartbeatCts.Token);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPrivacyWorkProcessor>();
            await processor.ProcessAsync(lease, _workerId, stoppingToken);
        }
        catch (Exception ex)
        {
            WorkProcessingFailed(_logger, lease.Kind, lease.WorkId, ex);
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IPrivacyWorkStore>();
            await store.AbandonLeaseAsync(
                lease,
                _workerId,
                PrivacyErrorCodes.WorkerUnavailable,
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
                // Expected after the work item reaches a terminal state.
            }
        }
    }

    private async Task HeartbeatAsync(PrivacyWorkLease lease, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.LeaseSeconds / 3));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IPrivacyWorkStore>();
            if (!await store.RenewLeaseAsync(lease, _workerId, LeaseDuration(), ct))
            {
                return;
            }
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
}
