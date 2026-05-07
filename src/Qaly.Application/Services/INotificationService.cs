using Qaly.Application.Common.Models;

namespace Qaly.Application.Services;

public interface INotificationService
{
    Task<Result<IReadOnlyList<NotificationDto>>> GetByUserAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default);
    Task<Result> MarkAsReadAsync(Guid id, CancellationToken ct = default);
    Task<Result> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
    Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task CreateAsync(Guid userId, string message, string type, Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default);
    Task BroadcastToProjectAsync(Guid projectId, string message, string eventType, object? payload = null, CancellationToken ct = default);
}

public record NotificationDto(
    Guid Id,
    string Message,
    string Type,
    bool IsRead,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    DateTimeOffset CreatedAt);
