using Qaly.Application.Services;

namespace Qaly.Application.Common.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);

    Task BroadcastToProjectAsync(Guid projectId, string message, string eventType, object? payload = null, CancellationToken ct = default);

    Task BroadcastToAllAsync(string message, string eventType, object? payload = null, CancellationToken ct = default);
}

public sealed class NullNotificationPublisher : INotificationPublisher
{
    public Task PublishAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task BroadcastToProjectAsync(Guid projectId, string message, string eventType, object? payload = null, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task BroadcastToAllAsync(string message, string eventType, object? payload = null, CancellationToken ct = default)
        => Task.CompletedTask;
}
