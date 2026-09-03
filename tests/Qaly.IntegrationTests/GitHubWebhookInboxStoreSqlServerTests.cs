using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities.GitHub;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Integrations.GitHub;

namespace Qaly.IntegrationTests;

public sealed class GitHubWebhookInboxStoreSqlServerTests
{
    [Fact]
    public async Task ClaimNextAsync_WithTwoWorkers_GrantsOneExclusiveLease()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var inboxId = await database.SeedPendingAsync();
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();

        var claims = await Task.WhenAll(
            new GitHubWebhookInboxStore(firstContext)
                .ClaimNextAsync("github-worker-a", TimeSpan.FromMinutes(2), 5),
            new GitHubWebhookInboxStore(secondContext)
                .ClaimNextAsync("github-worker-b", TimeSpan.FromMinutes(2), 5));

        claims.Count(claim => claim != null).Should().Be(1);
        await using var verification = database.CreateContext();
        var persisted = await verification.GitHubWebhookInbox.SingleAsync(item => item.Id == inboxId);
        persisted.Status.Should().Be(GitHubWebhookInboxStatuses.Processing);
        persisted.AttemptCount.Should().Be(1);
        persisted.LeaseOwner.Should().BeOneOf("github-worker-a", "github-worker-b");
        persisted.LeaseExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ExpiredLease_IsReclaimedAndStaleOwnerCannotCommitProcessorMutation()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var inboxId = await database.SeedPendingAsync();
        await using var staleContext = database.CreateContext();
        var staleStore = new GitHubWebhookInboxStore(staleContext);
        var staleClaim = await staleStore.ClaimNextAsync(
            "github-worker-stale", TimeSpan.FromMinutes(2), 5);
        staleClaim.Should().NotBeNull();

        await using (var expiryContext = database.CreateContext())
        {
            await expiryContext.GitHubWebhookInbox
                .Where(item => item.Id == inboxId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }

        await using var recoveryContext = database.CreateContext();
        var recoveryStore = new GitHubWebhookInboxStore(recoveryContext);
        var recovered = await recoveryStore.ClaimNextAsync(
            "github-worker-recovery", TimeSpan.FromMinutes(2), 5);
        recovered.Should().NotBeNull();
        recovered!.AttemptCount.Should().Be(2);

        staleClaim!.OrganizationId = Guid.NewGuid();
        var staleCommit = () => staleStore.CompleteAsync(staleClaim, "github-worker-stale");
        await staleCommit.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await recoveryStore.CompleteAsync(recovered, "github-worker-recovery");
        await using var verification = database.CreateContext();
        var persisted = await verification.GitHubWebhookInbox.SingleAsync(item => item.Id == inboxId);
        persisted.Status.Should().Be(GitHubWebhookInboxStatuses.Processed);
        persisted.AttemptCount.Should().Be(2);
        persisted.LeaseOwner.Should().BeNull();
        persisted.LeaseExpiresAt.Should().BeNull();
        persisted.OrganizationId.Should().BeNull("the stale processor mutation must roll back");
    }

    [Fact]
    public async Task RenewAndRelease_RequireTheCurrentLeaseOwner()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var inboxId = await database.SeedPendingAsync();
        await using var context = database.CreateContext();
        var store = new GitHubWebhookInboxStore(context);
        var claim = await store.ClaimNextAsync("github-worker-owner", TimeSpan.FromSeconds(30), 5);
        claim.Should().NotBeNull();
        var originalExpiry = claim!.LeaseExpiresAt!.Value;

        (await store.RenewLeaseAsync(
            inboxId, "github-worker-other", TimeSpan.FromMinutes(2))).Should().BeFalse();
        (await store.RenewLeaseAsync(
            inboxId, "github-worker-owner", TimeSpan.FromMinutes(2))).Should().BeTrue();
        await using (var renewedVerification = database.CreateContext())
        {
            var renewed = await renewedVerification.GitHubWebhookInbox
                .AsNoTracking()
                .SingleAsync(item => item.Id == inboxId);
            renewed.LeaseExpiresAt.Should().BeAfter(originalExpiry);
        }
        (await store.ReleaseLeaseAsync(inboxId, "github-worker-other")).Should().BeFalse();
        (await store.ReleaseLeaseAsync(inboxId, "github-worker-owner")).Should().BeTrue();

        await using var verification = database.CreateContext();
        var persisted = await verification.GitHubWebhookInbox.SingleAsync(item => item.Id == inboxId);
        persisted.Status.Should().Be(GitHubWebhookInboxStatuses.Pending);
        persisted.LeaseOwner.Should().BeNull();
        persisted.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task FailureBackoff_PreventsImmediateHotLoopAndAllowsLaterRetry()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var inboxId = await database.SeedPendingAsync();
        await using (var failureContext = database.CreateContext())
        {
            var store = new GitHubWebhookInboxStore(failureContext);
            var claim = await store.ClaimNextAsync("github-worker-failure", TimeSpan.FromMinutes(2), 5);
            await store.RecordFailureAsync(
                claim!,
                "github-worker-failure",
                "provider unavailable",
                TimeSpan.FromSeconds(30));
        }

        await using (var earlyContext = database.CreateContext())
        {
            var early = await new GitHubWebhookInboxStore(earlyContext)
                .ClaimNextAsync("github-worker-early", TimeSpan.FromMinutes(2), 5);
            early.Should().BeNull();
        }

        await using (var makeDueContext = database.CreateContext())
        {
            await makeDueContext.GitHubWebhookInbox
                .Where(item => item.Id == inboxId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }

        await using var retryContext = database.CreateContext();
        var retry = await new GitHubWebhookInboxStore(retryContext)
            .ClaimNextAsync("github-worker-retry", TimeSpan.FromMinutes(2), 5);
        retry.Should().NotBeNull();
        retry!.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExpiredFinalAttempt_IsTerminalizedAndNotReclaimed()
    {
        if (!SqlServerTestEnvironment.IsAvailable()) return;

        await using var database = await SqlTestDatabase.CreateAsync();
        var inboxId = await database.SeedPendingAsync();
        await using (var claimContext = database.CreateContext())
        {
            var claim = await new GitHubWebhookInboxStore(claimContext)
                .ClaimNextAsync("github-worker-final", TimeSpan.FromMinutes(2), 1);
            claim.Should().NotBeNull();
        }

        await using (var expiryContext = database.CreateContext())
        {
            await expiryContext.GitHubWebhookInbox
                .Where(item => item.Id == inboxId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }

        await using (var recoveryContext = database.CreateContext())
        {
            var recovered = await new GitHubWebhookInboxStore(recoveryContext)
                .ClaimNextAsync("github-worker-too-late", TimeSpan.FromMinutes(2), 1);
            recovered.Should().BeNull();
        }

        await using var verification = database.CreateContext();
        var persisted = await verification.GitHubWebhookInbox.SingleAsync(item => item.Id == inboxId);
        persisted.Status.Should().Be(GitHubWebhookInboxStatuses.Failed);
        persisted.AttemptCount.Should().Be(1);
        persisted.LeaseOwner.Should().BeNull();
        persisted.LeaseExpiresAt.Should().BeNull();
        persisted.LastError.Should().Contain("final permitted delivery attempt");
    }

    private sealed class SqlTestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private SqlTestDatabase(string connectionString) => _connectionString = connectionString;

        public static async Task<SqlTestDatabase> CreateAsync()
        {
            var databaseName = $"QalyGitHubInboxTests_{Guid.NewGuid():N}";
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
            await context.Database.MigrateAsync();
            return database;
        }

        public QalyDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<QalyDbContext>()
                .UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure())
                .Options;
            return new QalyDbContext(options);
        }

        public async Task<Guid> SeedPendingAsync()
        {
            await using var context = CreateContext();
            var inbox = new GitHubWebhookInbox
            {
                DeliveryId = $"delivery-{Guid.NewGuid():N}",
                EventName = "push",
                Payload = "{}",
                Status = GitHubWebhookInboxStatuses.Pending,
                ReceivedAt = DateTimeOffset.UtcNow
            };
            context.GitHubWebhookInbox.Add(inbox);
            await context.SaveChangesAsync();
            return inbox.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
