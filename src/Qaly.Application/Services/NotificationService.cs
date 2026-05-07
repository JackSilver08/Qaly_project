using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IRepository<Notification> _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notificationPublisher;

    public NotificationService(
        IRepository<Notification> notificationRepo,
        IUnitOfWork unitOfWork,
        INotificationPublisher notificationPublisher)
    {
        _notificationRepo = notificationRepo;
        _unitOfWork = unitOfWork;
        _notificationPublisher = notificationPublisher;
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
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var notification = new Notification
        {
            UserId = userId,
            Message = message.Trim(),
            Type = string.IsNullOrWhiteSpace(type) ? "Info" : type.Trim(),
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType
        };

        await _notificationRepo.AddAsync(notification, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _notificationPublisher.PublishAsync(userId, ToDto(notification), ct);
    }

    public Task BroadcastToProjectAsync(Guid projectId, string message, string eventType, object? payload = null, CancellationToken ct = default)
        => _notificationPublisher.BroadcastToProjectAsync(projectId, message, eventType, payload, ct);

    private static NotificationDto ToDto(Notification notification)
        => new(
            notification.Id,
            notification.Message,
            notification.Type,
            notification.IsRead,
            notification.RelatedEntityId,
            notification.RelatedEntityType,
            notification.CreatedAt);
}
