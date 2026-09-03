using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class WebhookOutboxProcessorTests
{
    [Fact]
    public async Task HealthCheck_EmptyOutbox_IsHealthy()
    {
        await using var db = new QalyDbContext(NewOptions());
        var health = new WebhookOutboxHealthCheck(db, NewHealthConfiguration());

        var result = await health.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["pending"].Should().Be(0);
        result.Data["deadLetters"].Should().Be(0);
    }

    [Fact]
    public async Task HealthCheck_DeadLetterOrStalePending_IsDegradedWithoutTakingAppOffline()
    {
        await using var db = new QalyDbContext(NewOptions());
        var stale = NewMessage();
        stale.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var deadLetter = NewMessage();
        deadLetter.DeadLetteredAt = DateTimeOffset.UtcNow;
        db.WebhookOutboxMessages.AddRange(stale, deadLetter);
        await db.SaveChangesAsync();
        var health = new WebhookOutboxHealthCheck(db, NewHealthConfiguration());

        var result = await health.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data["pending"].Should().Be(1);
        result.Data["deadLetters"].Should().Be(1);
        result.Data["oldestPendingMinutes"].Should().BeOfType<double>()
            .Which.Should().BeGreaterThanOrEqualTo(19);
    }

    [Fact]
    public async Task ProcessNextAsync_AfterFailedDelivery_RetriesSameOccurrenceAfterRestart()
    {
        var options = NewOptions();
        var message = NewMessage();
        await using (var seed = new QalyDbContext(options))
        {
            seed.WebhookOutboxMessages.Add(message);
            await seed.SaveChangesAsync();
        }

        var publisher = new Mock<IWebhookPublisher>();
        publisher
            .SetupSequence(service => service.PublishOutboxAsync(
                message.Id,
                message.ProjectId,
                message.EventType,
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookPublishReceipt(message.Id, 1, 0, ["endpoint:delivery_failed"]))
            .ReturnsAsync(new WebhookPublishReceipt(message.Id, 1, 1, []));

        await using (var firstRun = new QalyDbContext(options))
        {
            var processed = await new WebhookOutboxProcessor(firstRun, publisher.Object)
                .ProcessNextAsync("worker-before-restart");
            processed.Should().BeTrue();
        }

        await using (var arrangeRetry = new QalyDbContext(options))
        {
            var pending = await arrangeRetry.WebhookOutboxMessages.SingleAsync();
            pending.RetryCount.Should().Be(1);
            pending.ProcessedAt.Should().BeNull();
            pending.DeadLetteredAt.Should().BeNull();
            pending.LeaseOwner.Should().BeNull();
            pending.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await arrangeRetry.SaveChangesAsync();
        }

        await using (var restarted = new QalyDbContext(options))
        {
            var processed = await new WebhookOutboxProcessor(restarted, publisher.Object)
                .ProcessNextAsync("worker-after-restart");
            processed.Should().BeTrue();
        }

        await using (var verify = new QalyDbContext(options))
        {
            var completed = await verify.WebhookOutboxMessages.AsNoTracking().SingleAsync();
            completed.ProcessedAt.Should().NotBeNull();
            completed.DeadLetteredAt.Should().BeNull();
            completed.LeaseOwner.Should().BeNull();
        }
        publisher.Verify(service => service.PublishOutboxAsync(
            message.Id,
            message.ProjectId,
            message.EventType,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessNextAsync_AfterMaximumFailures_DeadLettersMessage()
    {
        var options = NewOptions();
        var message = NewMessage();
        await using var db = new QalyDbContext(options);
        db.WebhookOutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var publisher = new Mock<IWebhookPublisher>();
        publisher
            .Setup(service => service.PublishOutboxAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, string _, object _, CancellationToken _) =>
                new WebhookPublishReceipt(id, 1, 0, ["endpoint:delivery_failed"]));
        var processor = new WebhookOutboxProcessor(db, publisher.Object);

        for (var attempt = 0; attempt < WebhookOutboxProcessor.MaxRetryCount; attempt++)
        {
            (await processor.ProcessNextAsync("dead-letter-worker")).Should().BeTrue();
            var pending = await db.WebhookOutboxMessages.SingleAsync();
            if (pending.DeadLetteredAt == null)
            {
                pending.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }
        }

        var deadLetter = await db.WebhookOutboxMessages.AsNoTracking().SingleAsync();
        deadLetter.RetryCount.Should().Be(WebhookOutboxProcessor.MaxRetryCount);
        deadLetter.DeadLetteredAt.Should().NotBeNull();
        deadLetter.ProcessedAt.Should().BeNull();
        deadLetter.ErrorMessage.Should().Contain("delivery_failed");
    }

    [Fact]
    public async Task ProcessNextAsync_DoesNotStealActiveLease()
    {
        var options = NewOptions();
        var message = NewMessage();
        message.LeaseOwner = "another-worker";
        message.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(1);
        await using var db = new QalyDbContext(options);
        db.WebhookOutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var publisher = new Mock<IWebhookPublisher>();
        var processed = await new WebhookOutboxProcessor(db, publisher.Object)
            .ProcessNextAsync("competing-worker");

        processed.Should().BeFalse();
        publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessNextAsync_WhenHostStops_ReleasesClaimWithoutRecordingFailure()
    {
        var options = NewOptions();
        var message = NewMessage();
        await using var db = new QalyDbContext(options);
        db.WebhookOutboxMessages.Add(message);
        await db.SaveChangesAsync();
        using var stopping = new CancellationTokenSource();
        var publisher = new Mock<IWebhookPublisher>();
        publisher.Setup(service => service.PublishOutboxAsync(
                message.Id,
                message.ProjectId,
                message.EventType,
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, string _, object _, CancellationToken ct) =>
            {
                stopping.Cancel();
                return Task.FromCanceled<WebhookPublishReceipt>(ct);
            });
        var processor = new WebhookOutboxProcessor(db, publisher.Object);

        var act = () => processor.ProcessNextAsync("stopping-worker", stopping.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        db.ChangeTracker.Clear();
        var pending = await db.WebhookOutboxMessages.AsNoTracking().SingleAsync();
        pending.LeaseOwner.Should().BeNull();
        pending.LockedUntil.Should().BeNull();
        pending.RetryCount.Should().Be(0);
        pending.ErrorMessage.Should().BeNull();
        pending.DeadLetteredAt.Should().BeNull();
    }

    [Fact]
    public async Task RetentionCleanup_RemovesOnlyExpiredOperationalHistory()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new QalyDbContext(NewOptions());
        var webhook = new WebhookSubscription
        {
            ProjectId = Guid.NewGuid(),
            PayloadUrl = "https://hooks.example.test/qaly",
            Secret = "never-return-this",
            Events = "[\"task.updated\"]"
        };
        var oldDelivery = NewDelivery(webhook.Id, now.AddDays(-31));
        var recentDelivery = NewDelivery(webhook.Id, now.AddDays(-2));
        var oldProcessed = NewMessage();
        oldProcessed.CreatedAt = now.AddDays(-40);
        oldProcessed.ProcessedAt = now.AddDays(-31);
        var recentProcessed = NewMessage();
        recentProcessed.ProcessedAt = now.AddDays(-2);
        var oldDeadLetter = NewMessage();
        oldDeadLetter.CreatedAt = now.AddDays(-40);
        oldDeadLetter.DeadLetteredAt = now.AddDays(-31);
        oldDeadLetter.RetryCount = WebhookOutboxProcessor.MaxRetryCount;

        db.WebhookSubscriptions.Add(webhook);
        db.WebhookDeliveryLogs.AddRange(oldDelivery, recentDelivery);
        db.WebhookOutboxMessages.AddRange(oldProcessed, recentProcessed, oldDeadLetter);
        await db.SaveChangesAsync();

        // Advance the persisted operational records into their retention windows. Keeping
        // this as a second save also mirrors records aging after their initial delivery.
        oldDelivery.CreatedAt = now.AddDays(-31);
        recentDelivery.CreatedAt = now.AddDays(-2);
        await db.SaveChangesAsync();

        var result = await new WebhookOperationsRetentionService(db, NewRetentionConfiguration())
            .CleanupAsync(now);

        result.Should().Be(new WebhookRetentionResult(1, 1));
        (await db.WebhookDeliveryLogs.IgnoreQueryFilters().AsNoTracking().Select(log => log.Id).ToListAsync())
            .Should().Equal(recentDelivery.Id);
        (await db.WebhookOutboxMessages.AsNoTracking().Select(message => message.Id).ToListAsync())
            .Should().BeEquivalentTo([recentProcessed.Id, oldDeadLetter.Id]);
    }

    private static DbContextOptions<QalyDbContext> NewOptions()
        => new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase($"webhook-outbox-{Guid.NewGuid():N}")
            .Options;

    private static IConfiguration NewHealthConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OperationalHealth:WebhookOutboxPendingThreshold"] = "200",
                ["OperationalHealth:WebhookOutboxMaxPendingAgeMinutes"] = "15"
            })
            .Build();

    private static IConfiguration NewRetentionConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OperationalHealth:WebhookDeliveryRetentionDays"] = "30",
                ["OperationalHealth:WebhookProcessedOutboxRetentionDays"] = "30"
            })
            .Build();

    private static WebhookDeliveryLog NewDelivery(Guid webhookId, DateTimeOffset createdAt)
        => new()
        {
            WebhookId = webhookId,
            EventType = "task.updated",
            RequestPayload = "{\"id\":\"safe-test\"}",
            IsSuccess = true,
            CreatedAt = createdAt
        };

    private static WebhookOutboxMessage NewMessage()
        => new()
        {
            ProjectId = Guid.NewGuid(),
            EventType = "task.updated",
            Payload = "{\"id\":\"7e66ece5-f59c-4934-8f87-cb77ac7fb47d\"}",
            NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1)
        };
}
