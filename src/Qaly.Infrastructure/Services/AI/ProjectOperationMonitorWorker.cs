using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI;

public sealed class ProjectOperationMonitorWorker : BackgroundService
{
    private static readonly Action<ILogger, Exception?> MonitorFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(4501, nameof(MonitorFailed)),
        "Project operation monitor iteration failed.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectOperationMonitorWorker> _logger;

    public ProjectOperationMonitorWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectOperationMonitorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IProjectLaunchOrchestratorService>();
                await service.EvaluateDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                MonitorFailed(_logger, exception);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
