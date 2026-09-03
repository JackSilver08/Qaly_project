using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiActionComposerSqlServerTests
{
    [Fact]
    [Trait("TestId", "TEST-ACTION-SQL-COUNT-10")]
    public async Task ExplicitTenTaskRequest_PersistsCanonicalTasksInIsolatedSqlServer()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        var databaseName = $"QalyActionComposerTests_{Guid.NewGuid():N}";
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var builder = string.IsNullOrWhiteSpace(configured)
            ? new SqlConnectionStringBuilder
            {
                DataSource = "(localdb)\\MSSQLLocalDB",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                Encrypt = false
            }
            : new SqlConnectionStringBuilder(configured);
        builder.InitialCatalog = databaseName;
        builder.MultipleActiveResultSets = true;

        try
        {
            using var factory = IntegrationTestFactory.CreateWithSqlServer(builder.ConnectionString);
            var scenario = new AiActionComposerApiTests(factory);
            await scenario.ExplicitTenTaskRequest_ConfirmPersistsExactlyTenAndReplayCreatesNoDuplicate();
        }
        finally
        {
            var options = new DbContextOptionsBuilder<QalyDbContext>()
                .UseSqlServer(builder.ConnectionString)
                .Options;
            await using var cleanup = new QalyDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
