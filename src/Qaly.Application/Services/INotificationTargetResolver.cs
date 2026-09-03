using Qaly.Domain.Entities;

namespace Qaly.Application.Services;

public sealed record NotificationTargetAccess(bool IsVisible, string? TargetUrl = null);

public interface INotificationTargetResolver
{
    Task<NotificationTargetAccess> ResolveAsync(
        Notification notification,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationTargetAccess>> ResolveManyAsync(
        IReadOnlyList<Notification> notifications,
        Guid userId,
        CancellationToken cancellationToken = default);
}
