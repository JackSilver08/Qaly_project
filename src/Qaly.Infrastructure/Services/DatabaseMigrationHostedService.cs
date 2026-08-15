using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed partial class DatabaseMigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    public DatabaseMigrationHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DatabaseMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_configuration.GetValue<bool>("UseInMemoryDatabase"))
        {
            LogSkippingMigrations(_logger);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

        LogApplyingMigrations(_logger);
        await dbContext.Database.MigrateAsync(cancellationToken);
        LogMigrationsCompleted(_logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping database migrations because UseInMemoryDatabase is enabled.")]
    private static partial void LogSkippingMigrations(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying database migrations...")]
    private static partial void LogApplyingMigrations(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations completed.")]
    private static partial void LogMigrationsCompleted(ILogger logger);
}
