using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;

namespace Qaly.IntegrationTests;

public sealed class VectorSyncOutboxStoreSqlServerTests
{
    [Fact]
    public async Task ClaimNextAsync_WithTwoWorkers_GrantsOneExclusiveLease()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var aggregateId = Guid.NewGuid();
        var outboxId = await database.SeedAsync(VectorSyncEventTypes.TaskUpdated, VectorSyncAggregateTypes.Task, aggregateId);
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();

        var claims = await Task.WhenAll(
            new VectorSyncOutboxStore(firstContext)
                .ClaimNextAsync("vector-worker-a", TimeSpan.FromMinutes(2), 5),
            new VectorSyncOutboxStore(secondContext)
                .ClaimNextAsync("vector-worker-b", TimeSpan.FromMinutes(2), 5));

        claims.Count(claim => claim != null).Should().Be(1);
        await using var verification = database.CreateContext();
        var persisted = await verification.VectorSyncOutbox.SingleAsync(item => item.Id == outboxId);
        persisted.RetryCount.Should().Be(1);
        persisted.LeaseOwner.Should().BeOneOf("vector-worker-a", "vector-worker-b");
        persisted.LeaseExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ClaimNextAsync_SerializesEventsForOneAggregate()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var aggregateId = Guid.NewGuid();
        var firstId = await database.SeedAsync(VectorSyncEventTypes.TaskUpdated, VectorSyncAggregateTypes.Task, aggregateId);
        var secondId = await database.SeedAsync(VectorSyncEventTypes.TaskDeleted, VectorSyncAggregateTypes.Task, aggregateId);
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstStore = new VectorSyncOutboxStore(firstContext);
        var secondStore = new VectorSyncOutboxStore(secondContext);

        var first = await firstStore.ClaimNextAsync("vector-worker-first", TimeSpan.FromMinutes(2), 5);
        var overtakingAttempt = await secondStore.ClaimNextAsync("vector-worker-second", TimeSpan.FromMinutes(2), 5);

        first!.Id.Should().Be(firstId);
        overtakingAttempt.Should().BeNull("a later delete must not overtake the earlier update");

        await firstStore.CompleteAsync(first, "vector-worker-first");
        var second = await secondStore.ClaimNextAsync("vector-worker-second", TimeSpan.FromMinutes(2), 5);
        second!.Id.Should().Be(secondId);
    }

    [Fact]
    public async Task ExpiredLease_IsReclaimedAndStaleOwnerCannotComplete()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var outboxId = await database.SeedAsync(
            VectorSyncEventTypes.ProjectUpdated,
            VectorSyncAggregateTypes.Project,
            Guid.NewGuid());
        await using var staleContext = database.CreateContext();
        var staleStore = new VectorSyncOutboxStore(staleContext);
        var staleClaim = await staleStore.ClaimNextAsync("vector-worker-stale", TimeSpan.FromMinutes(2), 5);

        await using (var expiryContext = database.CreateContext())
        {
            await expiryContext.VectorSyncOutbox
                .Where(item => item.Id == outboxId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }

        await using var recoveryContext = database.CreateContext();
        var recoveryStore = new VectorSyncOutboxStore(recoveryContext);
        var recovered = await recoveryStore.ClaimNextAsync("vector-worker-recovery", TimeSpan.FromMinutes(2), 5);
        recovered.Should().NotBeNull();
        recovered!.RetryCount.Should().Be(2);

        var staleCompletion = () => staleStore.CompleteAsync(staleClaim!, "vector-worker-stale");
        await staleCompletion.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await recoveryStore.CompleteAsync(recovered, "vector-worker-recovery");
        await using var verification = database.CreateContext();
        var persisted = await verification.VectorSyncOutbox.SingleAsync(item => item.Id == outboxId);
        persisted.ProcessedAt.Should().NotBeNull();
        persisted.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task FailureBackoff_StopsHotLoopAndDeadLettersAtMaxAttempts()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var outboxId = await database.SeedAsync(
            VectorSyncEventTypes.CommentAdded,
            VectorSyncAggregateTypes.Comment,
            Guid.NewGuid());
        await using (var failureContext = database.CreateContext())
        {
            var store = new VectorSyncOutboxStore(failureContext);
            var first = await store.ClaimNextAsync("vector-worker-failure", TimeSpan.FromMinutes(2), 2);
            await store.RecordFailureAsync(
                first!, "vector-worker-failure", "provider unavailable", 2, TimeSpan.FromSeconds(30));
        }

        await using (var earlyContext = database.CreateContext())
        {
            var early = await new VectorSyncOutboxStore(earlyContext)
                .ClaimNextAsync("vector-worker-early", TimeSpan.FromMinutes(2), 2);
            early.Should().BeNull();
        }

        await using (var dueContext = database.CreateContext())
        {
            await dueContext.VectorSyncOutbox
                .Where(item => item.Id == outboxId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }

        await using (var finalContext = database.CreateContext())
        {
            var store = new VectorSyncOutboxStore(finalContext);
            var final = await store.ClaimNextAsync("vector-worker-final", TimeSpan.FromMinutes(2), 2);
            final!.RetryCount.Should().Be(2);
            await store.RecordFailureAsync(
                final, "vector-worker-final", "still unavailable", 2, TimeSpan.FromSeconds(30));
        }

        await using var verification = database.CreateContext();
        var persisted = await verification.VectorSyncOutbox.SingleAsync(item => item.Id == outboxId);
        persisted.DeadLetteredAt.Should().NotBeNull();
        persisted.ErrorMessage.Should().Be("still unavailable");
        persisted.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task InvalidEventContract_IsDeadLetteredInsteadOfFalseSuccess()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var outboxId = await database.SeedAsync("UnknownEvent", VectorSyncAggregateTypes.Task, Guid.NewGuid());
        await using var context = database.CreateContext();

        var claim = await new VectorSyncOutboxStore(context)
            .ClaimNextAsync("vector-worker", TimeSpan.FromMinutes(2), 5);

        claim.Should().BeNull();
        await using var verification = database.CreateContext();
        var persisted = await verification.VectorSyncOutbox.SingleAsync(item => item.Id == outboxId);
        persisted.ProcessedAt.Should().BeNull();
        persisted.DeadLetteredAt.Should().NotBeNull();
        persisted.ErrorMessage.Should().Contain("invalid event type");
    }

    [Fact]
    public async Task P026Migration_RequeuesLegacyMismatchedEventsAndDeadLettersUnknownEvents()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAtAsync(
            "20260903120000_P025GitHubWebhookInboxLeaseReliability");
        var taskOutboxId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var unknownOutboxId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var taskPayload = $$"""{"Id":"{{taskId}}"}""";
        await using (var legacyContext = database.CreateContext())
        {
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO [VectorSyncOutbox]
                    ([Id], [EventType], [Payload], [RetryCount], [ProcessedAt], [ErrorMessage], [CreatedAt], [UpdatedAt])
                VALUES
                    ({{taskOutboxId}}, N'TaskItemUpdated', {{taskPayload}}, 1, {{now}}, NULL, {{now.AddMinutes(-2)}}, NULL),
                    ({{unknownOutboxId}}, N'LegacyMystery', N'not-json', 1, {{now}}, NULL, {{now.AddMinutes(-1)}}, NULL);
                """);
        }

        await database.MigrateAsync();

        await using var verification = database.CreateContext();
        var taskEvent = await verification.VectorSyncOutbox
            .AsNoTracking()
            .SingleAsync(item => item.Id == taskOutboxId);
        taskEvent.EventType.Should().Be(VectorSyncEventTypes.TaskUpdated);
        taskEvent.AggregateType.Should().Be(VectorSyncAggregateTypes.Task);
        taskEvent.AggregateId.Should().Be(taskId);
        taskEvent.ProcessedAt.Should().BeNull("the old worker falsely completed this mismatched event");
        taskEvent.DeadLetteredAt.Should().BeNull();
        taskEvent.RetryCount.Should().Be(0);
        taskEvent.SequenceNumber.Should().BePositive();

        var unknownEvent = await verification.VectorSyncOutbox
            .AsNoTracking()
            .SingleAsync(item => item.Id == unknownOutboxId);
        unknownEvent.ProcessedAt.Should().BeNull();
        unknownEvent.DeadLetteredAt.Should().NotBeNull();
        unknownEvent.AggregateType.Should().Be("Invalid");
        unknownEvent.AggregateId.Should().Be(unknownOutboxId);
    }

    private sealed class SqlTestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private SqlTestDatabase(string connectionString) => _connectionString = connectionString;

        public static Task<SqlTestDatabase> CreateAsync()
            => CreateAtAsync();

        public static async Task<SqlTestDatabase> CreateAtAsync(string? targetMigration = null)
        {
            var databaseName = $"QalyVectorOutboxTests_{Guid.NewGuid():N}";
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
            var database = new SqlTestDatabase(builder.ConnectionString);
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync(targetMigration);
            return database;
        }

        public async Task MigrateAsync()
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public QalyDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<QalyDbContext>()
                .UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure())
                .Options;
            return new QalyDbContext(options);
        }

        public async Task<Guid> SeedAsync(string eventType, string aggregateType, Guid aggregateId)
        {
            await using var context = CreateContext();
            var item = new VectorSyncOutbox
            {
                EventType = eventType,
                AggregateType = aggregateType,
                AggregateId = aggregateId,
                Payload = $$"""{"Id":"{{aggregateId}}"}"""
            };
            context.VectorSyncOutbox.Add(item);
            await context.SaveChangesAsync();
            return item.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
