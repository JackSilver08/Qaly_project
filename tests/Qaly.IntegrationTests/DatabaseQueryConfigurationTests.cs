using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Infrastructure;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class DatabaseQueryConfigurationTests
{
    [Fact]
    public void SqlServerRuntime_UsesSplitQueriesForMultiCollectionGraphs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "false",
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=QalyQueryConfiguration;Trusted_Connection=True;TrustServerCertificate=True"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DbContextOptions<QalyDbContext>>();

        RelationalOptionsExtension.Extract(options).QuerySplittingBehavior
            .Should().Be(QuerySplittingBehavior.SplitQuery);
    }
}
