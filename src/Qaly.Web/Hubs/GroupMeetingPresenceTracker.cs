using System.Collections.Concurrent;

namespace Qaly.Web.Hubs;

public sealed class GroupMeetingPresenceTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<MeetingKey, MeetingPresence>> _connections = new();

    public bool TryJoin(string connectionId, Guid groupId, Guid meetingId, Guid? userId)
    {
        var meetings = _connections.GetOrAdd(connectionId, static _ => new());
        return meetings.TryAdd(
            new MeetingKey(groupId, meetingId),
            new MeetingPresence(groupId, meetingId, userId, connectionId));
    }

    public bool TryLeave(string connectionId, Guid groupId, Guid meetingId, out MeetingPresence? presence)
    {
        presence = null;
        if (!_connections.TryGetValue(connectionId, out var meetings))
        {
            return false;
        }

        return meetings.TryRemove(new MeetingKey(groupId, meetingId), out presence);
    }

    public IReadOnlyList<MeetingPresence> RemoveConnection(string connectionId)
    {
        return _connections.TryRemove(connectionId, out var meetings)
            ? meetings.Values.ToArray()
            : [];
    }

    private readonly record struct MeetingKey(Guid GroupId, Guid MeetingId);
}

public sealed record MeetingPresence(
    Guid GroupId,
    Guid MeetingId,
    Guid? UserId,
    string ConnectionId);
