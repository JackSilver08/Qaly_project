using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qaly.Infrastructure.Services;

public sealed partial class WebhookOperationsRetentionWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookOperationsRetentionWorker> _logger;

    public WebhookOperationsRetentionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookOperationsRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var retention = scope.ServiceProvider.GetRequiredService<WebhookOperationsRetentionService>();
                var result = await retention.CleanupAsync(DateTimeOffset.UtcNow, stoppingToken);
                CleanupCompleted(_logger, result.DeliveryLogsDeleted, result.ProcessedOutboxDeleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                CleanupFailed(_logger, exception);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Webhook operations retention completed. DeliveryLogsDeleted={DeliveryLogsDeleted}, ProcessedOutboxDeleted={ProcessedOutboxDeleted}.")]
    private static partial void CleanupCompleted(
        ILogger logger,
        int deliveryLogsDeleted,
        int processedOutboxDeleted);

    [LoggerMessage(Level = LogLevel.Error, Message = "Webhook operations retention failed.")]
    private static partial void CleanupFailed(ILogger logger, Exception exception);
}
