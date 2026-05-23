using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Qaly.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IRepository<Notification> _notificationRepo;
    private readonly IRepository<PushSubscription> _pushRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly Qaly.Application.Common.Interfaces.IPushSender _pushSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IRepository<Notification> notificationRepo,
        IRepository<PushSubscription> pushRepo,
        IUnitOfWork unitOfWork,
        INotificationPublisher notificationPublisher,
        Qaly.Application.Common.Interfaces.IPushSender pushSender,
        ILogger<NotificationService> logger)
    {
        _notificationRepo = notificationRepo;
        _pushRepo = pushRepo;
        _unitOfWork = unitOfWork;
        _notificationPublisher = notificationPublisher;
        _pushSender = pushSender;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<NotificationDto>>> GetByUserAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = _notificationRepo.GetQueryable()
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        var notifications = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<NotificationDto>>(notifications.Select(ToDto).ToList());
    }

    public async Task<Result> MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _notificationRepo.GetByIdAsync(id, ct);
        if (notification == null)
        {
            return Result.Failure("Notification was not found.", 404);
        }

        notification.IsRead = true;
        await _notificationRepo.UpdateAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var notifications = await _notificationRepo.GetQueryable()
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(ct);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            await _notificationRepo.UpdateAsync(notification, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await _notificationRepo.GetQueryable()
            .CountAsync(notification => notification.UserId == userId && !notification.IsRead, ct);

        return Result.Success(count);
    }

    public async Task CreateAsync(
        Guid userId,
        string message,
        string type,
        string tone = "info",
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalizedMessage = message.Trim();
        var normalizedType = string.IsNullOrWhiteSpace(type) ? "Info" : type.Trim();
        var normalizedTone = string.IsNullOrWhiteSpace(tone) ? "info" : tone.Trim().ToLowerInvariant();
        var normalizedEntityType = string.IsNullOrWhiteSpace(relatedEntityType) ? null : relatedEntityType.Trim();
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey) ??
            BuildIdempotencyKey(normalizedType, relatedEntityId, normalizedEntityType, normalizedMessage);

        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            var existing = await _notificationRepo.GetQueryable()
                .AsNoTracking()
                .AnyAsync(notification =>
                    notification.UserId == userId &&
                    notification.IdempotencyKey == normalizedKey, ct);

            if (existing)
            {
                _logger.LogInformation(
                    "Skipped duplicate notification {NotificationType} for user {UserId} with key {IdempotencyKey}.",
                    normalizedType,
                    userId,
                    normalizedKey);
                return;
            }
        }

        var notification = new Notification
        {
            UserId = userId,
            Message = normalizedMessage,
            Type = normalizedType,
            Tone = normalizedTone,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = normalizedEntityType,
            IdempotencyKey = normalizedKey
        };

        try
        {
            await _notificationRepo.AddAsync(notification, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            var duplicateExists = await _notificationRepo.GetQueryable()
                .AsNoTracking()
                .AnyAsync(item => item.UserId == userId && item.IdempotencyKey == normalizedKey, ct);
            if (!duplicateExists)
            {
                throw;
            }

            _logger.LogInformation(
                "Skipped duplicate notification {NotificationType} for user {UserId} after unique key conflict.",
                normalizedType,
                userId);
            return;
        }

        await _notificationPublisher.PublishAsync(userId, ToDto(notification), ct);
        await SendPushNotificationAsync(userId, "New Notification", normalizedMessage);
    }

    public Task CreateAsync(
        Guid userId,
        string message,
        string type,
        Guid? relatedEntityId,
        string? relatedEntityType,
        CancellationToken ct = default)
        => CreateAsync(userId, message, type, "info", relatedEntityId, relatedEntityType, null, ct);

    public Task BroadcastToProjectAsync(Guid projectId, string message, string eventType, object? payload = null, CancellationToken ct = default)
        => _notificationPublisher.BroadcastToProjectAsync(projectId, message, eventType, payload, ct);

    public async Task<Result> SubscribePushAsync(Guid userId, string endpoint, string p256dh, string auth)
    {
        var subscription = await _pushRepo.GetQueryable()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint);

        if (subscription == null)
        {
            subscription = new PushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth
            };
            await _pushRepo.AddAsync(subscription);
        }
        else
        {
            subscription.UserId = userId;
            subscription.P256dh = p256dh;
            subscription.Auth = auth;
            subscription.LastUsedAt = DateTimeOffset.UtcNow;
            await _pushRepo.UpdateAsync(subscription);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public Task SendPushNotificationAsync(Guid userId, string title, string message)
    {
        try
        {
            return _pushSender.SendAsync(userId, title, message, null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send push notification to user {UserId}", userId);
            return Task.CompletedTask;
        }
    }

    private static NotificationDto ToDto(Notification notification)
        => new(
            notification.Id,
            notification.Message,
            notification.Type,
            notification.Tone,
            notification.IsRead,
            notification.RelatedEntityId,
            notification.RelatedEntityType,
            notification.CreatedAt);

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var trimmed = idempotencyKey.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }

    private static string BuildIdempotencyKey(string type, Guid? relatedEntityId, string? relatedEntityType, string message)
    {
        var seed = $"{type}|{relatedEntityType}|{relatedEntityId}|{message}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seed)));
        return $"notification:{hash}";
    }
}
