using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Integrations.GitHub;

public sealed partial class GitHubWebhookWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<GitHubIntegrationOptions> _options;
    private readonly ILogger<GitHubWebhookWorker> _logger;

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

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<IGitHubWebhookProcessor>();
        var options = _options.CurrentValue;
        var items = await db.GitHubWebhookInbox
            .Where(x => x.Status == "Pending" || (x.Status == "Failed" && x.AttemptCount < options.MaxAttempts))
            .OrderBy(x => x.ReceivedAt).Take(Math.Max(1, options.BatchSize)).ToListAsync(ct);

        foreach (var item in items)
        {
            item.Status = "Processing";
            item.AttemptCount++;
            await db.SaveChangesAsync(ct);
            try
            {
                await processor.ProcessAsync(item, ct);
                item.Status = "Processed";
                item.ProcessedAt = DateTimeOffset.UtcNow;
                item.LastError = null;
            }
            catch (Exception ex)
            {
                item.Status = "Failed";
                item.LastError = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
                DeliveryFailed(_logger, item.DeliveryId, item.AttemptCount, ex);
            }
            await db.SaveChangesAsync(ct);
        }
        return items.Count;
    }

    private Task DelayAsync(CancellationToken ct)
        => Task.Delay(TimeSpan.FromMilliseconds(Math.Max(100, _options.CurrentValue.PollIntervalMilliseconds)), ct);

    [LoggerMessage(Level = LogLevel.Error, Message = "GitHub webhook worker loop failed.")]
    private static partial void WorkerLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub webhook delivery {DeliveryId} failed on attempt {Attempt}.")]
    private static partial void DeliveryFailed(ILogger logger, string deliveryId, int attempt, Exception exception);
}
