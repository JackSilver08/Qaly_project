using Microsoft.AspNetCore.SignalR;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Groups;

namespace Qaly.Web.Hubs;

public class SignalRGroupPollRealtimePublisher : IGroupPollRealtimePublisher
{
    private readonly IHubContext<GroupHub> _hubContext;

    public SignalRGroupPollRealtimePublisher(IHubContext<GroupHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishPollUpdatedAsync(Guid groupId, Guid pollId, GroupPollResultsDto results, DateTimeOffset updatedAt, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(GroupHub.WorkGroup(groupId))
            .SendAsync("PollUpdated", new
            {
                groupId,
                pollId,
                results,
                updatedAt
            }, ct);

    public Task PublishPollDeletedAsync(Guid groupId, Guid pollId, DateTimeOffset deletedAt, CancellationToken ct = default)
        => _hubContext.Clients
            .Group(GroupHub.WorkGroup(groupId))
            .SendAsync("PollDeleted", new
            {
                groupId,
                pollId,
                deletedAt
            }, ct);
}
