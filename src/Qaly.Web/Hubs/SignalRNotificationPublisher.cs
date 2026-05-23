using Microsoft.AspNetCore.SignalR;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using System.Text.Json;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Distributed;

namespace Qaly.Web.Hubs;

public class SignalRNotificationPublisher : INotificationPublisher
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
            _logger.LogInformation("Skipped duplicate realtime project event {EventType} for project {ProjectId}.", eventType, projectId);
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
            _logger.LogInformation("Skipped duplicate realtime system event {EventType}.", eventType);
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
                    _logger.LogError(
                        ex,
                        "Realtime event {EventName} failed after {Attempt} attempts. EventKey={EventKey}",
                        eventName,
                        attempt,
                        eventKey);
                    return false;
                }

                _logger.LogWarning(
                    ex,
                    "Realtime event {EventName} failed on attempt {Attempt}; retrying. EventKey={EventKey}",
                    eventName,
                    attempt,
                    eventKey);
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
            _logger.LogWarning(ex, "Failed to check distributed dedupe; falling back to allow send.");
            return false;
        }
    }

    private static string BuildRealtimeKey(string channel, Guid? projectId, string eventType, string message, object? payload)
    {
        var payloadJson = payload == null ? string.Empty : JsonSerializer.Serialize(payload);
        return $"{channel}:{projectId}:{eventType}:{message}:{payloadJson}";
    }
}
