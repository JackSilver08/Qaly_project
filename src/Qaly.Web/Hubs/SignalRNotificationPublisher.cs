using Microsoft.AspNetCore.SignalR;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;

namespace Qaly.Web.Hubs;

public class SignalRNotificationPublisher : INotificationPublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationPublisher(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
    {
        await _hubContext.Clients
            .Group(NotificationHub.UserGroup(userId.ToString()))
            .SendAsync("notificationReceived", notification, ct);
    }

    public async Task BroadcastToProjectAsync(Guid projectId, string message, string eventType, object? payload = null, CancellationToken ct = default)
    {
        await _hubContext.Clients
            .Group(NotificationHub.ProjectGroup(projectId.ToString()))
            .SendAsync("projectUpdated", new { message, eventType, projectId, payload }, ct);
    }

    public async Task BroadcastToAllAsync(string message, string eventType, object? payload = null, CancellationToken ct = default)
    {
        await _hubContext.Clients.All
            .SendAsync("systemUpdate", new { message, eventType, payload }, ct);
    }
}
