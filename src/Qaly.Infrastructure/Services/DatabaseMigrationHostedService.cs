using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services;

public sealed class DatabaseMigrationHostedService : IHostedService
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
            _logger.LogInformation("Skipping database migrations because UseInMemoryDatabase is enabled.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

        _logger.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Database migrations completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
