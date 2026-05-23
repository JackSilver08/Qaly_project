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

    public class WebPushSender : IPushSender
{
    private readonly IRepository<DomainPushSubscription> _pushRepo;
    private readonly ILogger<WebPushSender> _logger;
    private readonly VapidDetails? _vapid;

    private readonly IUnitOfWork _unitOfWork;

    public WebPushSender(IRepository<DomainPushSubscription> pushRepo, IUnitOfWork unitOfWork, IConfiguration configuration, ILogger<WebPushSender> logger)
    {
        _pushRepo = pushRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;

        var subject = configuration["Push:Subject"] ?? "mailto:admin@example.com";
        var publicKey = configuration["Push:VapidPublicKey"];
        var privateKey = configuration["Push:VapidPrivateKey"];
        if (!string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(privateKey))
        {
            _vapid = new VapidDetails(subject, publicKey, privateKey);
        }
        else
        {
            _logger.LogWarning("VAPID keys not configured: Push notifications will be disabled.");
        }
    }

    public async Task SendAsync(System.Guid userId, string title, string message, object? data = null, CancellationToken ct = default)
    {
        if (_vapid == null)
        {
            _logger.LogDebug("Skipping push send because VAPID keys are not configured.");
            return;
        }

        var subs = await _pushRepo.GetQueryable()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        if (subs.Count == 0)
        {
            _logger.LogDebug("No push subscriptions for user {UserId}", userId);
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
                await client.SendNotificationAsync(pushSub, payloadJson);
                sub.LastUsedAt = DateTimeOffset.UtcNow;
                await _pushRepo.UpdateAsync(sub, ct);
            }
            catch (WebPushException wex)
            {
                _logger.LogWarning(wex, "Web push failed for subscription {Endpoint}; removing if gone.", sub.Endpoint);
                if (wex.StatusCode == System.Net.HttpStatusCode.Gone || wex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    try
                    {
                        await _pushRepo.DeleteAsync(sub, ct);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send web push to {Endpoint}", sub.Endpoint);
            }
        }
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist push subscription updates");
        }
    }
}
