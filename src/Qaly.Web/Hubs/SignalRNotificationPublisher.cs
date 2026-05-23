using Microsoft.AspNetCore.SignalR;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using System.Text.Json;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Distributed;

namespace Qaly.Web.Hubs;

public partial class SignalRNotificationPublisher : INotificationPublisher
{
    private readonly TimeSpan DeduplicationWindow = TimeSpan.FromSeconds(30);

    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationPublisher> _logger;
    private readonly IConnectionMultiplexer _redis;

    public SignalRNotificationPublisher(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationPublisher> logger,
        IConnectionMultiplexer redis)
    {
        _hubContext = hubContext;
        _logger = logger;
        _redis = redis;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Skipped duplicate realtime project event {EventType} for project {ProjectId}.")]
    private static partial void LogSkippedDuplicateProjectEvent(ILogger logger, string eventType, Guid projectId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Skipped duplicate realtime system event {EventType}.")]
    private static partial void LogSkippedDuplicateSystemEvent(ILogger logger, string eventType);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Realtime event {EventName} failed after {Attempt} attempts. EventKey={EventKey}")]
    private static partial void LogRealtimeEventFailed(ILogger logger, Exception ex, string eventName, int attempt, string eventKey);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Realtime event {EventName} failed on attempt {Attempt}; retrying. EventKey={EventKey}")]
    private static partial void LogRealtimeEventAttemptFailed(ILogger logger, Exception ex, string eventName, int attempt, string eventKey);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Failed to check distributed dedupe; falling back to allow send.")]
    private static partial void LogDedupeCheckFailed(ILogger logger, Exception ex);

    public async Task PublishAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
    {
        await SendWithRetryAsync(
            () => _hubContext.Clients
                .Group(NotificationHub.UserGroup(userId.ToString()))
                .SendAsync("notificationReceived", notification, ct),
            "notificationReceived",
            $"notification:{notification.Id}",
            ct);
    }

    public async Task BroadcastToProjectAsync(Guid projectId, string message, string eventType, object? payload = null, CancellationToken ct = default)
    {
        var eventKey = BuildRealtimeKey("projectUpdated", projectId, eventType, message, payload);
        if (await IsDuplicateAsync(eventKey))
        {
            LogSkippedDuplicateProjectEvent(_logger, eventType, projectId);
            return;
        }

        var delivered = await SendWithRetryAsync(
            () => _hubContext.Clients
                .Group(NotificationHub.ProjectGroup(projectId.ToString()))
                .SendAsync("projectUpdated", new { eventId = eventKey, message, eventType, projectId, payload }, ct),
            "projectUpdated",
            eventKey,
            ct);
        if (!delivered)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(eventKey);
            }
            catch { }
        }
    }

    public async Task BroadcastToAllAsync(string message, string eventType, object? payload = null, CancellationToken ct = default)
    {
        var eventKey = BuildRealtimeKey("systemUpdate", null, eventType, message, payload);
        if (await IsDuplicateAsync(eventKey))
        {
            LogSkippedDuplicateSystemEvent(_logger, eventType);
            return;
        }

        var delivered = await SendWithRetryAsync(
            () => _hubContext.Clients.All
                .SendAsync("systemUpdate", new { eventId = eventKey, message, eventType, payload }, ct),
            "systemUpdate",
            eventKey,
            ct);
        if (!delivered)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(eventKey);
            }
            catch { }
        }
    }

    private async Task<bool> SendWithRetryAsync(Func<Task> send, string eventName, string eventKey, CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await send();
                return true;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                if (attempt == maxAttempts)
                {
                    LogRealtimeEventFailed(_logger, ex, eventName, attempt, eventKey);
                    return false;
                }

                LogRealtimeEventAttemptFailed(_logger, ex, eventName, attempt, eventKey);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
        }

        return false;
    }

    private static bool IsDuplicate(string eventKey)
    {
        throw new NotSupportedException();
    }

    private async Task<bool> IsDuplicateAsync(string eventKey)
    {
        try
        {
            var db = _redis.GetDatabase();
            // Attempt to set the key with NX (only set if not exists)
            var set = await db.StringSetAsync(eventKey, DateTimeOffset.UtcNow.ToString("o"), DeduplicationWindow, when: When.NotExists);
            return !set; // if set==false => duplicate
        }
        catch (Exception ex)
        {
            LogDedupeCheckFailed(_logger, ex);
            return false;
        }
    }

    private static string BuildRealtimeKey(string channel, Guid? projectId, string eventType, string message, object? payload)
    {
        var payloadJson = payload == null ? string.Empty : JsonSerializer.Serialize(payload);
        return $"{channel}:{projectId}:{eventType}:{message}:{payloadJson}";
    }
}
