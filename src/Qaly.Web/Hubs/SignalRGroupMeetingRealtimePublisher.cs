using Microsoft.AspNetCore.SignalR;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Web.Hubs;

public sealed class SignalRGroupMeetingRealtimePublisher : IGroupMeetingRealtimePublisher
{
    private readonly IHubContext<GroupHub> _hubContext;

    public SignalRGroupMeetingRealtimePublisher(IHubContext<GroupHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishMeetingStartedAsync(Guid groupId, GroupMeetingSessionDto meeting, CancellationToken ct = default)
    {
        var payload = BuildMeetingPayload(groupId, meeting);
        await _hubContext.Clients.Group(GroupHub.WorkGroup(groupId)).SendAsync("MeetingStarted", payload, ct);
        await _hubContext.Clients.Group(GroupHub.WorkGroup(groupId)).SendAsync("meetingStarted", payload, ct);
        await _hubContext.Clients.Group(GroupHub.MeetingGroup(groupId, meeting.Id)).SendAsync("meetingStarted", payload, ct);
    }

    public async Task PublishMeetingEndedAsync(Guid groupId, Guid meetingId, CancellationToken ct = default)
    {
        var payload = new
        {
            groupId,
            meetingId
        };

        await _hubContext.Clients.Group(GroupHub.WorkGroup(groupId)).SendAsync("MeetingEnded", payload, ct);
        await _hubContext.Clients.Group(GroupHub.WorkGroup(groupId)).SendAsync("meetingEnded", payload, ct);
        await _hubContext.Clients.Group(GroupHub.MeetingGroup(groupId, meetingId)).SendAsync("meetingEnded", payload, ct);
    }

    public async Task PublishParticipantJoinedAsync(Guid groupId, Guid meetingId, Guid? userId, string connectionId, CancellationToken ct = default)
    {
        var payload = BuildParticipantPayload(groupId, meetingId, userId, connectionId);
        await _hubContext.Clients.Group(GroupHub.WorkGroup(groupId)).SendAsync("meetingParticipantJoined", payload, ct);
        await _hubContext.Clients.Group(GroupHub.MeetingGroup(groupId, meetingId)).SendAsync("meetingParticipantJoined", payload, ct);
    }

    public async Task PublishParticipantLeftAsync(Guid groupId, Guid meetingId, Guid? userId, string connectionId, CancellationToken ct = default)
    {
        var payload = BuildParticipantPayload(groupId, meetingId, userId, connectionId);
        await _hubContext.Clients.Group(GroupHub.WorkGroup(groupId)).SendAsync("meetingParticipantLeft", payload, ct);
        await _hubContext.Clients.Group(GroupHub.MeetingGroup(groupId, meetingId)).SendAsync("meetingParticipantLeft", payload, ct);
    }

    private static object BuildMeetingPayload(Guid groupId, GroupMeetingSessionDto meeting)
        => new
        {
            groupId,
            meeting
        };

    private static object BuildParticipantPayload(Guid groupId, Guid meetingId, Guid? userId, string connectionId)
        => new
        {
            groupId,
            meetingId,
            userId,
            connectionId,
            at = DateTimeOffset.UtcNow
        };
}
