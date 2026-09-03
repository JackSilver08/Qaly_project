using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qaly.Infrastructure.Services;

public sealed partial class WebhookOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookOutboxWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public WebhookOutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerStarted(_logger, _workerId);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<WebhookOutboxProcessor>();
                var processed = await processor.ProcessNextAsync(_workerId, stoppingToken);
                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                WorkerFailed(_logger, exception);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        WorkerStopped(_logger, _workerId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook outbox worker {WorkerId} started.")]
    private static partial void WorkerStarted(ILogger logger, string workerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook outbox worker {WorkerId} stopped.")]
    private static partial void WorkerStopped(ILogger logger, string workerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Webhook outbox worker loop failed.")]
    private static partial void WorkerFailed(ILogger logger, Exception exception);
}
