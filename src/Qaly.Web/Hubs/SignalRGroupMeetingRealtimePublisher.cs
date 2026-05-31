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

    public Task PublishMeetingStartedAsync(Guid groupId, GroupMeetingSessionDto meeting, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(GroupHub.WorkGroup(groupId))
            .SendAsync("MeetingStarted", new
            {
                groupId,
                meeting
            }, ct);

    public Task PublishMeetingEndedAsync(Guid groupId, Guid meetingId, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(GroupHub.WorkGroup(groupId))
            .SendAsync("MeetingEnded", new
            {
                groupId,
                meetingId
            }, ct);
}
