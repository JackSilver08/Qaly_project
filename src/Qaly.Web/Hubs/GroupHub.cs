using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.Services;

namespace Qaly.Web.Hubs;

[Authorize]
public class GroupHub : Hub
{
    private readonly IGroupsService _groupsService;

    public GroupHub(IGroupsService groupsService)
    {
        _groupsService = groupsService;
    }

    public async Task JoinGroup(Guid groupId)
    {
        if (!await _groupsService.CanAccessGroupAsync(groupId, Context.ConnectionAborted))
        {
            throw new HubException("Access denied.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, WorkGroup(groupId), Context.ConnectionAborted);
        await Clients.Caller.SendAsync("groupJoined", new { groupId }, Context.ConnectionAborted);
    }

    public async Task LeaveGroup(Guid groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, WorkGroup(groupId), Context.ConnectionAborted);
        await Clients.Caller.SendAsync("groupLeft", new { groupId }, Context.ConnectionAborted);
    }

    public async Task SendMessage(Guid groupId, string content, string messageType = "Text")
    {
        var result = await _groupsService.CreateMessageAsync(
            groupId,
            new SendGroupMessageRequest(content, messageType),
            Context.ConnectionAborted);

        if (!result.IsSuccess || result.Data == null)
        {
            throw new HubException(result.Error ?? "Message could not be sent.");
        }

        await Clients
            .Group(WorkGroup(groupId))
            .SendAsync("groupMessageReceived", result.Data, Context.ConnectionAborted);
    }

    public async Task TypingStarted(Guid groupId)
    {
        await BroadcastPresenceSignalAsync(groupId, "typingStarted");
    }

    public async Task TypingStopped(Guid groupId)
    {
        await BroadcastPresenceSignalAsync(groupId, "typingStopped");
    }

    private async Task BroadcastPresenceSignalAsync(Guid groupId, string eventName)
    {
        if (!await _groupsService.CanAccessGroupAsync(groupId, Context.ConnectionAborted))
        {
            throw new HubException("Access denied.");
        }

        await Clients
            .OthersInGroup(WorkGroup(groupId))
            .SendAsync(eventName, new
            {
                groupId,
                connectionId = Context.ConnectionId,
                userId = Context.UserIdentifier
            }, Context.ConnectionAborted);
    }

    public static string WorkGroup(Guid groupId)
        => $"workgroup:{groupId}";
}
