using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Domain.Entities;
using Qaly.Domain.Entities.GitHub;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Integrations.GitHub;
using Qaly.Infrastructure.Services;
using Qaly.Infrastructure.Services.AI;
using Qaly.Infrastructure.Services.Privacy;

namespace Qaly.UnitTests;

public sealed class WorkerShutdownCancellationTests
{
    [Fact]
    public async Task AiJobCancellation_ReleasesLeaseWithoutRecordingProviderFailure()
    {
        using var stopping = new CancellationTokenSource();
        var processor = new Mock<IAiJobProcessor>();
        processor.Setup(item => item.ProcessAsync(
                It.IsAny<AiJobLease>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((AiJobLease _, string _, CancellationToken ct) =>
            {
                stopping.Cancel();
                return Task.FromCanceled(ct);
            });
        var store = new Mock<IAiJobDispatchStore>();
        var services = new ServiceCollection()
            .AddSingleton(processor.Object)
            .AddSingleton(store.Object)
            .BuildServiceProvider();
        var options = new Mock<IOptionsMonitor<AiJobPlatformOptions>>();
        options.SetupGet(item => item.CurrentValue).Returns(new AiJobPlatformOptions
        {
            LeaseSeconds = 120,
            HeartbeatSeconds = 30
        });
        var worker = new AiJobWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            options.Object,
            NullLogger<AiJobWorker>.Instance);
        var lease = new AiJobLease(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow.AddMinutes(2));

        var act = () => worker.ProcessLeaseAsync(lease, stopping.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        store.Verify(item => item.AbandonLeaseAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.ReleaseLeaseAsync(
            lease.DispatchId,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrivacyCancellation_ReleasesLeaseWithoutRecordingWorkerFailure()
    {
        using var stopping = new CancellationTokenSource();
        var processor = new Mock<IPrivacyWorkProcessor>();
        processor.Setup(item => item.ProcessAsync(
                It.IsAny<PrivacyWorkLease>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((PrivacyWorkLease _, string _, CancellationToken ct) =>
            {
                stopping.Cancel();
                return Task.FromCanceled(ct);
            });
        var store = new Mock<IPrivacyWorkStore>();
        var services = new ServiceCollection()
            .AddSingleton(processor.Object)
            .AddSingleton(store.Object)
            .BuildServiceProvider();
        var options = new Mock<IOptionsMonitor<PrivacyV4Options>>();
        options.SetupGet(item => item.CurrentValue).Returns(new PrivacyV4Options
        {
            LeaseSeconds = 120
        });
        var worker = new PrivacyWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            options.Object,
            NullLogger<PrivacyWorker>.Instance);
        var lease = new PrivacyWorkLease(
            PrivacyWorkKinds.Retention, Guid.NewGuid(), 1, DateTimeOffset.UtcNow.AddMinutes(2));

        var act = () => worker.ProcessLeaseAsync(lease, stopping.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        store.Verify(item => item.AbandonLeaseAsync(
            It.IsAny<PrivacyWorkLease>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.ReleaseLeaseAsync(
            lease,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiJobLeaseLoss_CancelsProcessorAndCannotAbandonNewOwnerLease()
    {
        var processorCanceled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new Mock<IAiJobProcessor>();
        processor.Setup(item => item.ProcessAsync(
                It.IsAny<AiJobLease>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (AiJobLease _, string _, CancellationToken ct) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
                catch (OperationCanceledException)
                {
                    processorCanceled.TrySetResult();
                    throw;
                }
            });
        var store = new Mock<IAiJobDispatchStore>();
        store.Setup(item => item.RenewLeaseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var services = new ServiceCollection()
            .AddSingleton(processor.Object)
            .AddSingleton(store.Object)
            .BuildServiceProvider();
        var options = new Mock<IOptionsMonitor<AiJobPlatformOptions>>();
        options.SetupGet(item => item.CurrentValue).Returns(new AiJobPlatformOptions
        {
            LeaseSeconds = 10,
            HeartbeatSeconds = 1
        });
        var worker = new AiJobWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            options.Object,
            NullLogger<AiJobWorker>.Instance);
        var lease = new AiJobLease(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow.AddMinutes(2));

        await worker.ProcessLeaseAsync(lease, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        await processorCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        store.Verify(item => item.AbandonLeaseAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.ReleaseLeaseAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrivacyLeaseLoss_CancelsProcessorAndCannotAbandonNewOwnerLease()
    {
        var processorCanceled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new Mock<IPrivacyWorkProcessor>();
        processor.Setup(item => item.ProcessAsync(
                It.IsAny<PrivacyWorkLease>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (PrivacyWorkLease _, string _, CancellationToken ct) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
                catch (OperationCanceledException)
                {
                    processorCanceled.TrySetResult();
                    throw;
                }
            });
        var store = new Mock<IPrivacyWorkStore>();
        store.Setup(item => item.RenewLeaseAsync(
                It.IsAny<PrivacyWorkLease>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var services = new ServiceCollection()
            .AddSingleton(processor.Object)
            .AddSingleton(store.Object)
            .BuildServiceProvider();
        var options = new Mock<IOptionsMonitor<PrivacyV4Options>>();
        options.SetupGet(item => item.CurrentValue).Returns(new PrivacyV4Options
        {
            LeaseSeconds = 10,
            HeartbeatSeconds = 1
        });
        var worker = new PrivacyWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            options.Object,
            NullLogger<PrivacyWorker>.Instance);
        var lease = new PrivacyWorkLease(
            PrivacyWorkKinds.Retention, Guid.NewGuid(), 1, DateTimeOffset.UtcNow.AddMinutes(2));

        await worker.ProcessLeaseAsync(lease, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        await processorCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        store.Verify(item => item.AbandonLeaseAsync(
            It.IsAny<PrivacyWorkLease>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.ReleaseLeaseAsync(
            It.IsAny<PrivacyWorkLease>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GitHubCancellation_ReleasesProcessingClaimWithoutRecordingFalseFailure()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var stopping = new CancellationTokenSource();
        var processor = new Mock<IGitHubWebhookProcessor>();
        processor.Setup(item => item.ProcessAsync(
                It.IsAny<GitHubWebhookInbox>(),
                It.IsAny<CancellationToken>()))
            .Returns((GitHubWebhookInbox _, CancellationToken ct) =>
            {
                stopping.Cancel();
                return Task.FromCanceled(ct);
            });
        var services = new ServiceCollection()
            .AddDbContext<QalyDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddScoped<IGitHubWebhookInboxStore, GitHubWebhookInboxStore>()
            .AddSingleton(processor.Object)
            .BuildServiceProvider();
        var options = new Mock<IOptionsMonitor<GitHubIntegrationOptions>>();
        options.SetupGet(item => item.CurrentValue).Returns(new GitHubIntegrationOptions
        {
            Enabled = true,
            WorkerEnabled = true,
            BatchSize = 1,
            MaxAttempts = 5,
            LeaseSeconds = 120,
            HeartbeatSeconds = 30
        });
        Guid inboxId;
        await using (var seedScope = services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var inbox = new GitHubWebhookInbox
            {
                DeliveryId = "shutdown-delivery",
                EventName = "push",
                Payload = "{}",
                Status = GitHubWebhookInboxStatuses.Pending
            };
            db.GitHubWebhookInbox.Add(inbox);
            await db.SaveChangesAsync();
            inboxId = inbox.Id;
        }
        var worker = new GitHubWebhookWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            options.Object,
            NullLogger<GitHubWebhookWorker>.Instance);

        var act = () => worker.ProcessBatchAsync(stopping.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await using var verifyScope = services.CreateAsyncScope();
        var persisted = await verifyScope.ServiceProvider
            .GetRequiredService<QalyDbContext>()
            .GitHubWebhookInbox.AsNoTracking()
            .SingleAsync(item => item.Id == inboxId);
        persisted.Status.Should().Be(GitHubWebhookInboxStatuses.Pending);
        persisted.LastError.Should().BeNull();
        persisted.AttemptCount.Should().Be(1);
        persisted.LeaseOwner.Should().BeNull();
        persisted.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task VectorSyncCancellation_PropagatesAndReleasesClaimWithoutFalseFailure()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var stopping = new CancellationTokenSource();
        var aggregateId = Guid.NewGuid();
        var ingestion = new Mock<IAiIngestionService>();
        ingestion.Setup(service => service.SyncTaskAsync(
                aggregateId,
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, CancellationToken ct) =>
            {
                stopping.Cancel();
                return Task.FromCanceled(ct);
            });
        var services = new ServiceCollection()
            .AddDbContext<QalyDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddScoped<IVectorSyncOutboxStore, VectorSyncOutboxStore>()
            .AddSingleton(ingestion.Object)
            .BuildServiceProvider();
        var options = new Mock<IOptionsMonitor<VectorSyncOptions>>();
        options.SetupGet(item => item.CurrentValue).Returns(new VectorSyncOptions
        {
            BatchSize = 1,
            MaxAttempts = 5,
            LeaseSeconds = 120,
            HeartbeatSeconds = 30
        });
        Guid outboxId;
        await using (var seedScope = services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var item = new VectorSyncOutbox
            {
                EventType = VectorSyncEventTypes.TaskUpdated,
                AggregateType = VectorSyncAggregateTypes.Task,
                AggregateId = aggregateId,
                Payload = "{}"
            };
            db.VectorSyncOutbox.Add(item);
            await db.SaveChangesAsync();
            outboxId = item.Id;
        }
        var worker = new VectorSyncWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            options.Object,
            NullLogger<VectorSyncWorker>.Instance);

        var act = () => worker.ProcessBatchAsync(stopping.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await using var verifyScope = services.CreateAsyncScope();
        var persisted = await verifyScope.ServiceProvider
            .GetRequiredService<QalyDbContext>()
            .VectorSyncOutbox.AsNoTracking()
            .SingleAsync(item => item.Id == outboxId);
        persisted.ProcessedAt.Should().BeNull();
        persisted.DeadLetteredAt.Should().BeNull();
        persisted.ErrorMessage.Should().BeNull();
        persisted.RetryCount.Should().Be(1);
        persisted.LeaseOwner.Should().BeNull();
        persisted.LeaseExpiresAt.Should().BeNull();
    }
}
