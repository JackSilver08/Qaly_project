using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Domain.Interfaces;
using DomainPushSubscription = Qaly.Domain.Entities.PushSubscription;
using System.Text.Json;
using WebPush;

namespace Qaly.Infrastructure.Services;

public partial class WebPushSender : IPushSender
{
    private static int _disabledNoticeLogged;
    private static int _missingKeyWarningLogged;
    private readonly IRepository<DomainPushSubscription> _pushRepo;
    private readonly ILogger<WebPushSender> _logger;
    private readonly VapidDetails? _vapid;

    private readonly IUnitOfWork _unitOfWork;

    public WebPushSender(IRepository<DomainPushSubscription> pushRepo, IUnitOfWork unitOfWork, IConfiguration configuration, ILogger<WebPushSender> logger)
    {
        _pushRepo = pushRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;

        var pushEnabled = configuration.GetValue<bool?>("Push:Enabled") ?? true;
        var subject = configuration["Push:Subject"] ?? "mailto:admin@example.com";
        var publicKey = configuration["Push:VapidPublicKey"];
        var privateKey = configuration["Push:VapidPrivateKey"];
        if (!pushEnabled)
        {
            if (Interlocked.Exchange(ref _disabledNoticeLogged, 1) == 0)
            {
                LogPushDisabled(_logger);
            }
        }
        else if (!string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(privateKey))
        {
            _vapid = new VapidDetails(subject, publicKey, privateKey);
        }
        else if (Interlocked.Exchange(ref _missingKeyWarningLogged, 1) == 0)
        {
            LogVapidNotConfigured(_logger);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "VAPID keys not configured: Push notifications will be disabled.")]
    private static partial void LogVapidNotConfigured(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Skipping push send because VAPID keys are not configured.")]
    private static partial void LogSkippingPushSendNoVapid(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "No push subscriptions for user {UserId}")]
    private static partial void LogNoPushSubscriptions(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Web push failed for subscription {Endpoint}; removing if gone.")]
    private static partial void LogWebPushFailed(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Failed to send web push to {Endpoint}")]
    private static partial void LogFailedSendWebPush(ILogger logger, Exception ex, string endpoint);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Failed to persist push subscription updates")]
    private static partial void LogFailedPersistPushUpdates(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Web push is explicitly disabled for this environment.")]
    private static partial void LogPushDisabled(ILogger logger);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "Failed to remove expired push subscription {Endpoint}.")]
    private static partial void LogFailedRemoveExpiredSubscription(ILogger logger, Exception ex, string endpoint);

    public async Task SendAsync(System.Guid userId, string title, string message, object? data = null, CancellationToken ct = default)
    {
        if (_vapid == null)
        {
            LogSkippingPushSendNoVapid(_logger);
            return;
        }

        var subs = await _pushRepo.GetQueryable()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        if (subs.Count == 0)
        {
            LogNoPushSubscriptions(_logger, userId);
            return;
        }

        var client = new WebPushClient();
        client.SetVapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);

        var payloadObj = new { title, message, data };
        var payloadJson = JsonSerializer.Serialize(payloadObj);

        foreach (var sub in subs)
        {
            try
            {
                var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payloadJson, cancellationToken: ct);
                sub.LastUsedAt = DateTimeOffset.UtcNow;
                await _pushRepo.UpdateAsync(sub, ct);
            }
            catch (WebPushException wex)
            {
                LogWebPushFailed(_logger, wex, sub.Endpoint);
                if (wex.StatusCode == System.Net.HttpStatusCode.Gone || wex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    try
                    {
                        await _pushRepo.DeleteAsync(sub, ct);
                    }
                    catch (Exception deleteException)
                    {
                        LogFailedRemoveExpiredSubscription(_logger, deleteException, sub.Endpoint);
                    }
                }
            }
            catch (Exception ex)
            {
                LogFailedSendWebPush(_logger, ex, sub.Endpoint);
            }
        }
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            LogFailedPersistPushUpdates(_logger, ex);
        }
    }
}
