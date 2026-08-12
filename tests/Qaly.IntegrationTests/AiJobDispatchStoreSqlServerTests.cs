using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.IntegrationTests;

public sealed class AiJobDispatchStoreSqlServerTests
{
    [Fact]
    public async Task ClaimNextAsync_WithTwoWorkers_GrantsOneExclusiveLease()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        await using var database = await SqlTestDatabase.CreateAsync();
        var jobId = await database.SeedQueuedJobAsync();

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstStore = new AiJobDispatchStore(firstContext);
        var secondStore = new AiJobDispatchStore(secondContext);

        var claims = await Task.WhenAll(
            firstStore.ClaimNextAsync("worker-a", TimeSpan.FromMinutes(2)),
            secondStore.ClaimNextAsync("worker-b", TimeSpan.FromMinutes(2)));

        claims.Count(claim => claim != null).Should().Be(1);
        await using var verification = database.CreateContext();
        var job = await verification.AiJobs.SingleAsync(item => item.Id == jobId);
        var dispatch = await verification.AiJobDispatches.SingleAsync(item => item.AiJobId == jobId);
        var attempts = await verification.AiProviderAttempts.Where(item => item.AiJobId == jobId).ToListAsync();
        job.Status.Should().Be(AiJobStatuses.Running);
        job.AttemptCount.Should().Be(1);
        dispatch.DeliveryCount.Should().Be(1);
        dispatch.LeaseOwner.Should().BeOneOf("worker-a", "worker-b");
        attempts.Should().ContainSingle(item => item.AttemptNumber == 1 && item.Status == AiAttemptStatuses.Running);
    }

    [Fact]
    public async Task ClaimNextAsync_AfterLeaseExpiry_RecordsFailedAttemptAndReclaimsJob()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        await using var database = await SqlTestDatabase.CreateAsync();
        var jobId = await database.SeedQueuedJobAsync();

        await using (var firstContext = database.CreateContext())
        {
            var firstLease = await new AiJobDispatchStore(firstContext)
                .ClaimNextAsync("worker-a", TimeSpan.FromMinutes(2));
            firstLease.Should().NotBeNull();
        }

        await using (var expiryContext = database.CreateContext())
        {
            var dispatch = await expiryContext.AiJobDispatches.SingleAsync(item => item.AiJobId == jobId);
            dispatch.LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await expiryContext.SaveChangesAsync();
        }

        AiJobLease? recoveredLease;
        await using (var recoveryContext = database.CreateContext())
        {
            recoveredLease = await new AiJobDispatchStore(recoveryContext)
                .ClaimNextAsync("worker-b", TimeSpan.FromMinutes(2));
        }

        recoveredLease.Should().NotBeNull();
        recoveredLease!.AttemptNumber.Should().Be(2);
        await using var verification = database.CreateContext();
        var job = await verification.AiJobs.SingleAsync(item => item.Id == jobId);
        var dispatchAfterRecovery = await verification.AiJobDispatches.SingleAsync(item => item.AiJobId == jobId);
        var attempts = await verification.AiProviderAttempts
            .Where(item => item.AiJobId == jobId)
            .OrderBy(item => item.AttemptNumber)
            .ToListAsync();
        job.Status.Should().Be(AiJobStatuses.Running);
        job.AttemptCount.Should().Be(2);
        dispatchAfterRecovery.DeliveryCount.Should().Be(2);
        dispatchAfterRecovery.LeaseOwner.Should().Be("worker-b");
        attempts.Should().HaveCount(2);
        attempts[0].Status.Should().Be(AiAttemptStatuses.Failed);
        attempts[0].ErrorCode.Should().Be("AI_WORKER_LEASE_EXPIRED");
        attempts[1].Status.Should().Be(AiAttemptStatuses.Running);
    }

    [Fact]
    public async Task RenewLeaseAsync_ExtendsOnlyTheCurrentWorkerLease()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        await using var database = await SqlTestDatabase.CreateAsync();
        var jobId = await database.SeedQueuedJobAsync();

        DateTimeOffset originalExpiry;
        await using (var workerContext = database.CreateContext())
        {
            var store = new AiJobDispatchStore(workerContext);
            var lease = await store.ClaimNextAsync("worker-heartbeat", TimeSpan.FromSeconds(30));
            lease.Should().NotBeNull();
            originalExpiry = lease!.LeaseExpiresAt;

            (await store.RenewLeaseAsync(lease.DispatchId, "other-worker", TimeSpan.FromMinutes(2)))
                .Should().BeFalse();
            (await store.RenewLeaseAsync(lease.DispatchId, "worker-heartbeat", TimeSpan.FromMinutes(2)))
                .Should().BeTrue();
        }

        await using var verification = database.CreateContext();
        var dispatch = await verification.AiJobDispatches.SingleAsync(item => item.AiJobId == jobId);
        dispatch.LeaseOwner.Should().Be("worker-heartbeat");
        dispatch.LeaseExpiresAt.Should().BeAfter(originalExpiry);
    }

    [Fact]
    public async Task AbandonLeaseAsync_SchedulesBackoffAndPreventsEarlyReclaim()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        await using var database = await SqlTestDatabase.CreateAsync();
        var jobId = await database.SeedQueuedJobAsync();

        await using (var firstContext = database.CreateContext())
        {
            var store = new AiJobDispatchStore(firstContext);
            var lease = await store.ClaimNextAsync("worker-a", TimeSpan.FromMinutes(2));
            lease.Should().NotBeNull();
            await store.AbandonLeaseAsync(
                lease!.DispatchId,
                "worker-a",
                AiErrorCodes.ProviderUnavailable,
                "provider unavailable");
        }

        await using (var earlyContext = database.CreateContext())
        {
            var earlyClaim = await new AiJobDispatchStore(earlyContext)
                .ClaimNextAsync("worker-b", TimeSpan.FromMinutes(2));
            earlyClaim.Should().BeNull();
        }

        await using (var verification = database.CreateContext())
        {
            var job = await verification.AiJobs.SingleAsync(item => item.Id == jobId);
            var dispatch = await verification.AiJobDispatches.SingleAsync(item => item.AiJobId == jobId);
            var attempt = await verification.AiProviderAttempts.SingleAsync(item => item.AiJobId == jobId);
            job.Status.Should().Be(AiJobStatuses.Retrying);
            job.NextRetryAt.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(3));
            dispatch.LeaseOwner.Should().BeNull();
            dispatch.CompletedAt.Should().BeNull();
            attempt.Status.Should().Be(AiAttemptStatuses.Failed);
            attempt.ErrorCode.Should().Be(AiErrorCodes.ProviderUnavailable);

            job.AvailableAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            job.NextRetryAt = job.AvailableAt;
            dispatch.AvailableAt = job.AvailableAt;
            await verification.SaveChangesAsync();
        }

        await using var recoveryContext = database.CreateContext();
        var recovered = await new AiJobDispatchStore(recoveryContext)
            .ClaimNextAsync("worker-b", TimeSpan.FromMinutes(2));
        recovered.Should().NotBeNull();
        recovered!.AttemptNumber.Should().Be(2);
    }

    [Fact]
    public async Task ProcessAsync_WhenJobCanceledAfterClaim_CompletesAttemptWithoutGatewayMutation()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        await using var database = await SqlTestDatabase.CreateAsync();
        var jobId = await database.SeedQueuedJobAsync();
        AiJobLease lease;

        await using (var claimContext = database.CreateContext())
        {
            lease = (await new AiJobDispatchStore(claimContext)
                .ClaimNextAsync("worker-cancel", TimeSpan.FromMinutes(2)))!;
        }

        await using (var cancelContext = database.CreateContext())
        {
            var job = await cancelContext.AiJobs.SingleAsync(item => item.Id == jobId);
            job.Status = AiJobStatuses.Canceled;
            job.CancellationRequestedAt = DateTimeOffset.UtcNow;
            await cancelContext.SaveChangesAsync();
        }

        await using (var processorContext = database.CreateContext())
        {
            var (processor, gateway) = CreateProcessor(processorContext, new AiResponse
            {
                IsSuccess = true,
                Content = "{\"title\":\"must not be used\"}"
            });
            await processor.ProcessAsync(lease, "worker-cancel");
            gateway.Verify(
                item => item.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        await using var verification = database.CreateContext();
        var canceledJob = await verification.AiJobs.Include(item => item.Dispatch).SingleAsync(item => item.Id == jobId);
        var attempt = await verification.AiProviderAttempts.SingleAsync(item => item.AiJobId == jobId);
        canceledJob.Status.Should().Be(AiJobStatuses.Canceled);
        canceledJob.Dispatch!.CompletedAt.Should().NotBeNull();
        canceledJob.Dispatch.LeaseOwner.Should().BeNull();
        attempt.Status.Should().Be(AiAttemptStatuses.Canceled);
        (await verification.AiGeneratedDrafts.CountAsync(item => item.AiJobId == jobId)).Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateDelivery_PersistsOneDraftAndOneProviderCall()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        await using var database = await SqlTestDatabase.CreateAsync();
        var jobId = await database.SeedQueuedJobAsync();
        AiJobLease lease;

        await using (var claimContext = database.CreateContext())
        {
            lease = (await new AiJobDispatchStore(claimContext)
                .ClaimNextAsync("worker-once", TimeSpan.FromMinutes(2)))!;
        }

        await using (var processorContext = database.CreateContext())
        {
            var (processor, gateway) = CreateProcessor(processorContext, new AiResponse
            {
                IsSuccess = true,
                Content = "{\"title\":\"Create one task\",\"source_refs\":[]}",
                ProviderName = "Ollama",
                ModelName = "sql-test"
            });
            await processor.ProcessAsync(lease, "worker-once");
            await processor.ProcessAsync(lease, "worker-once");
            gateway.Verify(
                item => item.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        await using var verification = database.CreateContext();
        var job = await verification.AiJobs.Include(item => item.Dispatch).SingleAsync(item => item.Id == jobId);
        job.Status.Should().Be(AiJobStatuses.Succeeded);
        job.Dispatch!.CompletedAt.Should().NotBeNull();
        (await verification.AiProviderAttempts.CountAsync(item => item.AiJobId == jobId)).Should().Be(1);
        (await verification.AiGeneratedDrafts.CountAsync(item => item.AiJobId == jobId)).Should().Be(1);
    }

    private static (AiJobProcessor Processor, Mock<IAiGateway> Gateway) CreateProcessor(
        QalyDbContext context,
        AiResponse response)
    {
        var gateway = new Mock<IAiGateway>();
        gateway
            .Setup(item => item.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        var sourceGuard = new Mock<IAiSourceGuard>();
        sourceGuard
            .Setup(item => item.ValidateAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Qaly.Application.DTOs.Ai.AiJobSourceInputDto>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSourceGuardResult(true));
        var compliance = new Mock<IAiComplianceService>();
        var options = new Mock<IOptionsMonitor<AiJobPlatformOptions>>();
        options.SetupGet(item => item.CurrentValue).Returns(new AiJobPlatformOptions
        {
            Enabled = true,
            WorkerEnabled = true,
            BaseRetrySeconds = 1
        });

        return (
            new AiJobProcessor(
                context,
                gateway.Object,
                sourceGuard.Object,
                compliance.Object,
                options.Object,
                NullLogger<AiJobProcessor>.Instance),
            gateway);
    }

    private sealed class SqlTestDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private SqlTestDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static async Task<SqlTestDatabase> CreateAsync()
        {
            var databaseName = $"QalyAiJobTests_{Guid.NewGuid():N}";
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

        public async Task<Guid> SeedQueuedJobAsync()
        {
            await using var context = CreateContext();
            var user = new User
            {
                FullName = "AI Worker Test",
                Email = $"worker-{Guid.NewGuid():N}@qaly.test",
                PasswordHash = "not-used",
                Role = "Admin"
            };
            var project = new Project
            {
                Name = "AI Job Test",
                Code = $"AI-{Guid.NewGuid():N}"[..12],
                OwnerId = user.Id
            };
            var job = new AiJob
            {
                JobType = "task_draft",
                ProjectId = project.Id,
                SourceType = "manual",
                SchemaId = "task_draft.v4",
                SchemaVersion = "4.0",
                RequestJson = "{}",
                RequestHash = new string('a', 64),
                IdempotencyKey = $"test:{Guid.NewGuid():N}",
                Status = AiJobStatuses.Queued,
                AvailableAt = DateTimeOffset.UtcNow,
                MaxAttempts = 3,
                CacheKey = $"test:{Guid.NewGuid():N}",
                RequestedById = user.Id
            };
            context.Users.Add(user);
            context.Projects.Add(project);
            context.AiJobs.Add(job);
            context.AiJobDispatches.Add(new AiJobDispatch
            {
                AiJobId = job.Id,
                AvailableAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
            return job.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
