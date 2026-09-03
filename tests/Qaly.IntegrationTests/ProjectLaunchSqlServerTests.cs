using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class ProjectLaunchSqlServerTests
{
    [Fact]
    [Trait("TestId", "TEST-AI-P09-P10-SQL-RETRY-TRANSACTION-01")]
    public async Task ConfirmProjectLaunch_WithRetryingExecutionStrategy_PersistsOnceAndReadsBack()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

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
        builder.InitialCatalog = $"QalyProjectLaunchTests_{Guid.NewGuid():N}";
        builder.MultipleActiveResultSets = true;

        try
        {
            // Build the SQL Server schema through migrations. EnsureCreated generates
            // provider-specific cascade paths that SQL Server rejects, while production
            // also runs the migration model rather than EnsureCreated.
            var migrationOptions = new DbContextOptionsBuilder<QalyDbContext>()
                .UseSqlServer(builder.ConnectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(QalyDbContext).Assembly.FullName);
                    sql.EnableRetryOnFailure();
                })
                .Options;
            await using (var migrationDb = new QalyDbContext(migrationOptions))
            {
                await migrationDb.Database.MigrateAsync();
            }

            using var factory = IntegrationTestFactory.CreateWithSqlServer(builder.ConnectionString);
            var scenario = new AiAssistantTurnApiTests(factory);
            try
            {
                await scenario.P10IdempotencyReadBack_ReturnsSameReceiptAndDoesNotDuplicateCanonicalGraph();
            }
            finally
            {
                await scenario.DisposeAsync();
            }
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
