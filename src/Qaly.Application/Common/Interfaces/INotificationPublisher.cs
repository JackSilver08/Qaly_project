using Qaly.Application.Services;

namespace Qaly.Application.Common.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);
}

public sealed class NullNotificationPublisher : INotificationPublisher
{
    public Task PublishAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
        => Task.CompletedTask;
}
