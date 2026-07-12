using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiJobMigrationSqlServerTests
{
    private const string PreviousMigration = "20260623014453_AddGroupMemberReadState";

    [Fact]
    public async Task P002Migration_BackfillsCanonicalJobAndClassifiesEveryLegacyQueueRow()
    {
        await using var database = await SqlMigrationDatabase.CreateAsync();
        await using (var legacyContext = database.CreateContext())
        {
            await legacyContext.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            var user = new User
            {
                FullName = "AI Migration Test",
                Email = $"migration-{Guid.NewGuid():N}@qaly.test",
                PasswordHash = "not-used",
                Role = "Admin"
            };
            var project = new Project
            {
                Name = "AI Migration Project",
                Code = $"AM-{Guid.NewGuid():N}"[..12],
                OwnerId = user.Id
            };
            legacyContext.Users.Add(user);
            await legacyContext.SaveChangesAsync();

            // The current model contains TaskSequence, but this test intentionally
            // stops before the later AddTaskKeyNumbering migration.
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO [Projects] ([Id], [Name], [Code], [Status], [OwnerId], [CreatedAt])
                VALUES ({{project.Id}}, {{project.Name}}, {{project.Code}}, N'Active', {{user.Id}}, {{DateTimeOffset.UtcNow}});
                """);

            var legacyJobId = Guid.NewGuid();
            var activeQueueId = Guid.NewGuid();
            var terminalQueueId = Guid.NewGuid();
            var sourceId = Guid.NewGuid().ToString();
            var now = DateTimeOffset.UtcNow;
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO [AiJobs] (
                    [Id], [JobType], [ProjectId], [SourceType], [SourceId], [ProviderHint], [Sensitive], [Status],
                    [EstimatedCostUsd], [CacheKey], [RequestedById], [CreatedAt], [UpdatedAt]
                ) VALUES (
                    {{legacyJobId}}, N'task_draft', {{project.Id}}, N'task', {{sourceId}}, N'auto', 0, N'DraftReady',
                    0.01, N'legacy-cache', {{user.Id}}, {{now}}, NULL
                );

                INSERT INTO [AiJobQueue] (
                    [Id], [TenantId], [ProjectId], [RequestedBy], [JobType], [SchemaId], [Status], [Priority],
                    [Sensitive], [ConsentId], [ProviderHint], [InputRefType], [InputRefId], [PayloadJson], [ResultJson],
                    [RetryCount], [MaxRetry], [ErrorCode], [ErrorMessage], [EstimatedCostUsd], [StartedAt], [FinishedAt],
                    [CanceledAt], [CanceledBy], [CreatedAt], [UpdatedAt]
                ) VALUES
                    ({{activeQueueId}}, NULL, {{project.Id}}, {{user.Id}}, N'legacy-active', N'legacy.v1', N'queued', 100,
                     0, NULL, N'auto', NULL, NULL, N'{"input":"active"}', NULL, 0, 1, NULL, NULL, 0, NULL, NULL,
                     NULL, NULL, {{now}}, NULL),
                    ({{terminalQueueId}}, NULL, {{project.Id}}, {{user.Id}}, N'legacy-terminal', N'legacy.v1', N'succeeded', 100,
                     0, NULL, N'auto', NULL, NULL, N'{"input":"terminal"}', N'{"ok":true}', 0, 1, NULL, NULL, 0, NULL, {{now}},
                     NULL, NULL, {{now}}, NULL);
                """);

            await legacyContext.Database.ExecuteSqlRawAsync(ReadScript("p002-preflight.sql"));
            await legacyContext.GetService<IMigrator>().MigrateAsync();
        }

        await using var verification = database.CreateContext();
        var migratedJob = await verification.AiJobs.Include(job => job.Sources)
            .SingleAsync(job => job.LegacyStatus == "DraftReady");
        migratedJob.Status.Should().Be(AiJobStatuses.Succeeded);
        migratedJob.SchemaId.Should().Be("legacy.unresolved");
        migratedJob.RequestHash.Should().HaveLength(64);
        migratedJob.IdempotencyKey.Should().NotBeNullOrWhiteSpace();
        migratedJob.Sources.Should().ContainSingle();

        var records = await verification.AiJobMigrationRecords.OrderBy(record => record.Classification).ToListAsync();
        records.Should().HaveCount(2);
        records.Should().ContainSingle(record => record.Classification == "quarantined");
        records.Should().ContainSingle(record => record.Classification == "historical_unlinked");
        records.Should().OnlyContain(record => record.CanonicalAiJobId == null && record.LegacySnapshotJson != null);

        await verification.Database.ExecuteSqlRawAsync(ReadScript("p002-postflight.sql"));
    }

    private static string ReadScript(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "scripts", "ai-jobs", fileName));
        return File.ReadAllText(path);
    }

    private sealed class SqlMigrationDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private SqlMigrationDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static Task<SqlMigrationDatabase> CreateAsync()
        {
            var databaseName = $"QalyAiMigrationTests_{Guid.NewGuid():N}";
            var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            var builder = string.IsNullOrWhiteSpace(configured)
                ? new SqlConnectionStringBuilder
                {
                    DataSource = "(localdb)\\MSSQLLocalDB",
                    InitialCatalog = databaseName,
                    IntegratedSecurity = true,
                    TrustServerCertificate = true,
                    MultipleActiveResultSets = true
                }
                : new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
            return Task.FromResult(new SqlMigrationDatabase(builder.ConnectionString));
        }

        public QalyDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<QalyDbContext>()
                .UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure())
                .Options;
            return new QalyDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
