using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Common.Interfaces;

public interface IGroupMeetingRealtimePublisher
{
    Task PublishMeetingStartedAsync(Guid groupId, GroupMeetingSessionDto meeting, CancellationToken ct = default);

    Task PublishMeetingEndedAsync(Guid groupId, Guid meetingId, CancellationToken ct = default);

    Task PublishParticipantJoinedAsync(Guid groupId, Guid meetingId, Guid? userId, string connectionId, CancellationToken ct = default);

    Task PublishParticipantLeftAsync(Guid groupId, Guid meetingId, Guid? userId, string connectionId, CancellationToken ct = default);
}

public sealed class NullGroupMeetingRealtimePublisher : IGroupMeetingRealtimePublisher
{
    public Task PublishMeetingStartedAsync(Guid groupId, GroupMeetingSessionDto meeting, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PublishMeetingEndedAsync(Guid groupId, Guid meetingId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PublishParticipantJoinedAsync(Guid groupId, Guid meetingId, Guid? userId, string connectionId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PublishParticipantLeftAsync(Guid groupId, Guid meetingId, Guid? userId, string connectionId, CancellationToken ct = default)
        => Task.CompletedTask;
}
