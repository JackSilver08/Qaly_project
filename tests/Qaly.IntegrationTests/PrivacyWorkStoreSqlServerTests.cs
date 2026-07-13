using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.Privacy;

namespace Qaly.IntegrationTests;

public sealed class PrivacyWorkStoreSqlServerTests
{
    [Fact]
    public async Task ClaimNextAsync_WithTwoWorkers_GrantsOneExclusiveRetentionLease()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var actionId = await database.SeedRetentionActionAsync();

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var claims = await Task.WhenAll(
            database.CreateStore(firstContext).ClaimNextAsync("privacy-a", TimeSpan.FromMinutes(2)),
            database.CreateStore(secondContext).ClaimNextAsync("privacy-b", TimeSpan.FromMinutes(2)));

        claims.Count(claim => claim != null).Should().Be(1);
        claims.Single(claim => claim != null)!.Kind.Should().Be(PrivacyWorkKinds.Retention);
        await using var verification = database.CreateContext();
        var action = await verification.PrivacyRetentionActions.SingleAsync(item => item.Id == actionId);
        action.Status.Should().Be(PrivacyWorkerStatuses.Running);
        action.AttemptCount.Should().Be(1);
        action.LeaseOwner.Should().BeOneOf("privacy-a", "privacy-b");
    }

    [Fact]
    public async Task ClaimNextAsync_AfterDsarLeaseExpiry_RecoversAndReclaimsRequest()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var requestId = await database.SeedDataSubjectRequestAsync();

        await using (var firstContext = database.CreateContext())
        {
            var first = await database.CreateStore(firstContext).ClaimNextAsync("privacy-a", TimeSpan.FromMinutes(2));
            first.Should().NotBeNull();
            first!.Kind.Should().Be(PrivacyWorkKinds.DataSubjectRequest);
        }

        await using (var expiryContext = database.CreateContext())
        {
            var request = await expiryContext.DataSubjectRequests.SingleAsync(item => item.Id == requestId);
            request.LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await expiryContext.SaveChangesAsync();
        }

        PrivacyWorkLease? recovered;
        await using (var recoveryContext = database.CreateContext())
        {
            recovered = await database.CreateStore(recoveryContext).ClaimNextAsync("privacy-b", TimeSpan.FromMinutes(2));
        }

        recovered.Should().NotBeNull();
        recovered!.Kind.Should().Be(PrivacyWorkKinds.DataSubjectRequest);
        recovered.AttemptNumber.Should().Be(2);
        await using var verification = database.CreateContext();
        var finalRequest = await verification.DataSubjectRequests.SingleAsync(item => item.Id == requestId);
        finalRequest.Status.Should().Be(DataSubjectRequestStatuses.Collecting);
        finalRequest.AttemptCount.Should().Be(2);
        finalRequest.LeaseOwner.Should().Be("privacy-b");
        finalRequest.LastErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task AbandonLeaseAsync_SchedulesBackoffAndBlocksEarlyReclaim()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        var requestId = await database.SeedDataSubjectRequestAsync();
        PrivacyWorkLease lease;

        await using (var context = database.CreateContext())
        {
            var store = database.CreateStore(context);
            lease = (await store.ClaimNextAsync("privacy-a", TimeSpan.FromMinutes(2)))!;
            await store.AbandonLeaseAsync(
                lease,
                "privacy-a",
                PrivacyErrorCodes.WorkerUnavailable,
                "temporary vector outage");
        }

        await using (var earlyContext = database.CreateContext())
        {
            var early = await database.CreateStore(earlyContext).ClaimNextAsync("privacy-b", TimeSpan.FromMinutes(2));
            early.Should().BeNull();
        }

        await using var verification = database.CreateContext();
        var request = await verification.DataSubjectRequests.SingleAsync(item => item.Id == requestId);
        request.Status.Should().Be(DataSubjectRequestStatuses.Accepted);
        request.AvailableAt.Should().BeAfter(DateTimeOffset.UtcNow);
        request.LastErrorCode.Should().Be(PrivacyErrorCodes.WorkerUnavailable);
        request.LeaseOwner.Should().BeNull();
    }

    private sealed class SqlTestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly PrivacyV4Options _options = new()
        {
            Enabled = true,
            WorkerEnabled = true,
            BaseRetrySeconds = 30
        };

        private SqlTestDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static async Task<SqlTestDatabase> CreateAsync()
        {
            var databaseName = $"QalyPrivacyWorkTests_{Guid.NewGuid():N}";
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
            => new(new DbContextOptionsBuilder<QalyDbContext>()
                .UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure())
                .Options);

        public PrivacyWorkStore CreateStore(QalyDbContext context)
            => new(context, Options.Create(_options));

        public async Task<Guid> SeedRetentionActionAsync()
        {
            await using var context = CreateContext();
            var tenantId = Guid.NewGuid();
            var policy = new RetentionPolicy
            {
                TenantId = tenantId,
                Name = "SQL retention policy",
                DataClassification = PrivacyDataClasses.SensitiveCollaboration,
                Purpose = PrivacyPurposes.MeetingActionExtraction,
                AllowedRetentionDaysJson = "[30]",
                DefaultRetentionDays = 30,
                ExpiryAction = PrivacyExpiryActions.Redact,
                PolicyVersion = $"sql-{Guid.NewGuid():N}",
                CreatedById = Guid.NewGuid()
            };
            var action = new PrivacyRetentionAction
            {
                TenantId = tenantId,
                RetentionPolicyId = policy.Id,
                EntityType = nameof(MeetingImport),
                EntityId = Guid.NewGuid(),
                ActionType = PrivacyExpiryActions.Redact,
                Status = PrivacyWorkerStatuses.Pending,
                DueAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                MaxAttempts = 5
            };
            context.RetentionPolicies.Add(policy);
            context.PrivacyRetentionActions.Add(action);
            await context.SaveChangesAsync();
            return action.Id;
        }

        public async Task<Guid> SeedDataSubjectRequestAsync()
        {
            await using var context = CreateContext();
            var request = new DataSubjectRequest
            {
                TenantId = Guid.NewGuid(),
                RequesterUserId = Guid.NewGuid(),
                SubjectUserId = Guid.NewGuid(),
                RequestType = DataSubjectRequestTypes.Export,
                ScopeJson = "{\"scope\":\"all\"}",
                Status = DataSubjectRequestStatuses.Accepted,
                RequestedAt = DateTimeOffset.UtcNow,
                AvailableAt = DateTimeOffset.UtcNow.AddSeconds(-1),
                MaxAttempts = 5
            };
            context.DataSubjectRequests.Add(request);
            await context.SaveChangesAsync();
            return request.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
