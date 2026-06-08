using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Qaly.Web.Hubs;

namespace Qaly.IntegrationTests;

public sealed class GroupMeetingRealtimeTests
{
    [Fact]
    public void PresenceTracker_DuplicateJoinAndLeave_AreIdempotent()
    {
        var tracker = new GroupMeetingPresenceTracker();
        var groupId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.TryJoin("connection-1", groupId, meetingId, userId).Should().BeTrue();
        tracker.TryJoin("connection-1", groupId, meetingId, userId).Should().BeFalse();

        tracker.TryLeave("connection-1", groupId, meetingId, out var presence).Should().BeTrue();
        presence!.UserId.Should().Be(userId);
        tracker.TryLeave("connection-1", groupId, meetingId, out _).Should().BeFalse();
    }

    [Fact]
    public void PresenceTracker_RemoveConnection_ReturnsAllJoinedMeetingsOnce()
    {
        var tracker = new GroupMeetingPresenceTracker();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.TryJoin("connection-1", groupId, Guid.NewGuid(), userId).Should().BeTrue();
        tracker.TryJoin("connection-1", groupId, Guid.NewGuid(), userId).Should().BeTrue();

        tracker.RemoveConnection("connection-1").Should().HaveCount(2);
        tracker.RemoveConnection("connection-1").Should().BeEmpty();
    }

    [Fact]
    public async Task ParticipantPublisher_SendsOneEventAcrossWorkGroupAndMeetingGroup()
    {
        var proxy = new Mock<IClientProxy>();
        proxy
            .Setup(client => client.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients
            .Setup(value => value.Groups(It.IsAny<IReadOnlyList<string>>()))
            .Returns(proxy.Object);

        var hubContext = new Mock<IHubContext<GroupHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var publisher = new SignalRGroupMeetingRealtimePublisher(hubContext.Object);
        await publisher.PublishParticipantJoinedAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "connection-1");

        proxy.Verify(client => client.SendCoreAsync(
            "meetingParticipantJoined",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
