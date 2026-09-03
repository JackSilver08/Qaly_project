using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiNativeDomainActionsSqlServerTests
{
    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-DOMAIN-SQL-01")]
    public async Task LatestMigration_ChecklistAndFourSubtasksPersistAndReadBackOnSqlServer()
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
        builder.InitialCatalog = $"QalyNativeDomainTests_{Guid.NewGuid():N}";
        builder.MultipleActiveResultSets = true;

        try
        {
            using var factory = IntegrationTestFactory.CreateWithSqlServer(builder.ConnectionString);
            var scenario = new AiNativeDomainActionsApiTests(factory);
            await scenario.P16P17_ChecklistAndFourSubtasks_ConfirmCanonicalRowsSkillsDependenciesReadBackAndReplay();
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
