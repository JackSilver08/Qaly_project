using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Qaly.Web.Hubs;
using StackExchange.Redis;

namespace Qaly.IntegrationTests;

public sealed class SignalRNotificationPublisherTests
{
    [Fact]
    public async Task BroadcastToProjectAsync_CompletesOnlyAfterRealtimeDeliveryCompletes()
    {
        var delivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(client => client.SendCoreAsync(
                "projectUpdated",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(delivery.Task);

        var clients = new Mock<IHubClients>();
        clients.Setup(value => value.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var (redis, _) = CreateRedis(deduplicationAccepted: true);
        var publisher = new SignalRNotificationPublisher(
            hubContext.Object,
            Mock.Of<ILogger<SignalRNotificationPublisher>>(),
            redis.Object);

        var publish = publisher.BroadcastToProjectAsync(Guid.NewGuid(), "changed", "task.updated");
        publish.IsCompleted.Should().BeFalse("the publisher contract must represent actual delivery completion");

        delivery.SetResult();
        await publish;

        proxy.Verify(client => client.SendCoreAsync(
            "projectUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastToAllAsync_WhenDuplicate_DoesNotSend()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.SetupGet(value => value.All).Returns(proxy.Object);
        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var (redis, _) = CreateRedis(deduplicationAccepted: false);
        var publisher = new SignalRNotificationPublisher(
            hubContext.Object,
            Mock.Of<ILogger<SignalRNotificationPublisher>>(),
            redis.Object);

        await publisher.BroadcastToAllAsync("changed", "system.updated");

        proxy.Verify(client => client.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BroadcastToProjectAsync_WhenDeliveryFails_ReleasesDeduplicationKeyForRetry()
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(client => client.SendCoreAsync(
                "projectUpdated",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("realtime unavailable"));

        var clients = new Mock<IHubClients>();
        clients.Setup(value => value.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var (redis, database) = CreateRedis(deduplicationAccepted: true);
        database.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var publisher = new SignalRNotificationPublisher(
            hubContext.Object,
            Mock.Of<ILogger<SignalRNotificationPublisher>>(),
            redis.Object);

        await publisher.BroadcastToProjectAsync(Guid.NewGuid(), "changed", "task.updated");

        proxy.Verify(client => client.SendCoreAsync(
            "projectUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        database.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastToProjectAsync_WhenCanceled_DoesNotStartDelivery()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(value => value.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);
        var (redis, database) = CreateRedis(deduplicationAccepted: true);
        var publisher = new SignalRNotificationPublisher(
            hubContext.Object,
            Mock.Of<ILogger<SignalRNotificationPublisher>>(),
            redis.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => publisher.BroadcastToProjectAsync(
            Guid.NewGuid(), "changed", "task.updated", ct: cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        redis.Verify(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
        proxy.Verify(client => client.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<IConnectionMultiplexer> Redis, Mock<IDatabase> Database) CreateRedis(bool deduplicationAccepted)
    {
        var database = new Mock<IDatabase>();
        database.SetReturnsDefault(Task.FromResult(deduplicationAccepted));
        database.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                When.NotExists,
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(deduplicationAccepted);
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        return (redis, database);
    }
}
