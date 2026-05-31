using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Common.Interfaces;

public interface IGroupMeetingRealtimePublisher
{
    Task PublishMeetingStartedAsync(Guid groupId, GroupMeetingSessionDto meeting, CancellationToken ct = default);

    Task PublishMeetingEndedAsync(Guid groupId, Guid meetingId, CancellationToken ct = default);
}

public sealed class NullGroupMeetingRealtimePublisher : IGroupMeetingRealtimePublisher
{
    public Task PublishMeetingStartedAsync(Guid groupId, GroupMeetingSessionDto meeting, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PublishMeetingEndedAsync(Guid groupId, Guid meetingId, CancellationToken ct = default)
        => Task.CompletedTask;
}
