using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.Services;

namespace Qaly.Web.Hubs;

[Authorize]
public class GroupHub : Hub
{
    private readonly IGroupsService _groupsService;
    private readonly IGroupMeetingRealtimePublisher _meetingRealtimePublisher;

    public GroupHub(IGroupsService groupsService, IGroupMeetingRealtimePublisher meetingRealtimePublisher)
    {
        _groupsService = groupsService;
        _meetingRealtimePublisher = meetingRealtimePublisher;
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

    public async Task UpdateMessage(Guid groupId, Guid messageId, string content)
    {
        var result = await _groupsService.UpdateMessageAsync(
            groupId,
            messageId,
            new UpdateGroupMessageRequest(content),
            Context.ConnectionAborted);

        await BroadcastMessageChangeAsync(groupId, result);
    }

    public async Task RecallMessage(Guid groupId, Guid messageId)
    {
        var result = await _groupsService.RecallMessageAsync(
            groupId,
            messageId,
            Context.ConnectionAborted);

        await BroadcastMessageChangeAsync(groupId, result);
    }

    public async Task SetMessagePin(Guid groupId, Guid messageId, bool isPinned)
    {
        var result = await _groupsService.SetMessagePinAsync(
            groupId,
            messageId,
            new SetGroupMessagePinRequest(isPinned),
            Context.ConnectionAborted);

        await BroadcastMessageChangeAsync(groupId, result);
    }

    public async Task TypingStarted(Guid groupId)
    {
        await BroadcastPresenceSignalAsync(groupId, "typingStarted");
    }

    public async Task TypingStopped(Guid groupId)
    {
        await BroadcastPresenceSignalAsync(groupId, "typingStopped");
    }

    public async Task SendSignal(Guid groupId, object payload)
    {
        if (!await _groupsService.CanAccessGroupAsync(groupId, Context.ConnectionAborted))
        {
            throw new HubException("Access denied.");
        }

        await Clients
            .OthersInGroup(WorkGroup(groupId))
            .SendAsync("peerSignal", new
            {
                groupId,
                from = Context.ConnectionId,
                payload
            }, Context.ConnectionAborted);
    }

    public async Task JoinMeeting(Guid groupId, Guid meetingId)
    {
        var result = await _groupsService.JoinMeetingSessionAsync(groupId, meetingId, Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            throw new HubException(result.Error ?? "Meeting could not be joined.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, MeetingGroup(groupId, meetingId), Context.ConnectionAborted);
        await _meetingRealtimePublisher.PublishParticipantJoinedAsync(
            groupId,
            meetingId,
            CurrentUserId(),
            Context.ConnectionId,
            Context.ConnectionAborted);
    }

    public async Task LeaveMeeting(Guid groupId, Guid meetingId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, MeetingGroup(groupId, meetingId), Context.ConnectionAborted);
        await _meetingRealtimePublisher.PublishParticipantLeftAsync(
            groupId,
            meetingId,
            CurrentUserId(),
            Context.ConnectionId,
            Context.ConnectionAborted);
    }

    public async Task SendMeetingSignal(Guid groupId, Guid meetingId, object payload)
    {
        var result = await _groupsService.JoinMeetingSessionAsync(groupId, meetingId, Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            throw new HubException(result.Error ?? "Meeting signal could not be sent.");
        }

        await Clients
            .OthersInGroup(MeetingGroup(groupId, meetingId))
            .SendAsync("meetingPeerSignal", new
            {
                groupId,
                meetingId,
                from = Context.ConnectionId,
                userId = CurrentUserId(),
                payload
            }, Context.ConnectionAborted);
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

    private async Task BroadcastMessageChangeAsync(Guid groupId, Result<GroupMessageDto> result)
    {
        if (!result.IsSuccess || result.Data == null)
        {
            throw new HubException(result.Error ?? "Message could not be updated.");
        }

        await Clients
            .Group(WorkGroup(groupId))
            .SendAsync("groupMessageChanged", result.Data, Context.ConnectionAborted);
    }

    public static string WorkGroup(Guid groupId)
        => $"workgroup:{groupId}";

    public static string MeetingGroup(Guid groupId, Guid meetingId)
        => $"workgroup:{groupId}:meeting:{meetingId}";

    private Guid? CurrentUserId()
    {
        var raw =
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            Context.User?.FindFirstValue("sub");

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }
}
