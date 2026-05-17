using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qaly.Infrastructure.Services;

public partial class WebhookPublisher : IWebhookPublisher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRepository<WebhookSubscription> _webhookRepo;
    private readonly IRepository<WebhookDeliveryLog> _logRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WebhookPublisher> _logger;

    public WebhookPublisher(
        IHttpClientFactory httpClientFactory,
        IRepository<WebhookSubscription> webhookRepo,
        IRepository<WebhookDeliveryLog> logRepo,
        IUnitOfWork unitOfWork,
        ILogger<WebhookPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _webhookRepo = webhookRepo;
        _logRepo = logRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task PublishAsync(Guid projectId, string eventType, object payload, CancellationToken ct = default)
    {
        var webhooks = await _webhookRepo.GetQueryable()
            .Where(w => w.ProjectId == projectId && w.IsActive)
            .ToListAsync(ct);

        foreach (var webhook in webhooks)
        {
            var events = DeserializeEvents(webhook.Events);
            if (events.Contains(eventType) || events.Contains("*"))
            {
                _ = DispatchToWebhookSafeAsync(webhook, eventType, payload, ct);
            }
        }
    }

    private async Task DispatchToWebhookSafeAsync(WebhookSubscription webhook, string eventType, object payload, CancellationToken ct)
    {
        try
        {
            await DispatchToWebhookAsync(webhook, eventType, payload, ct);
        }
        catch (Exception ex)
        {
            LogWebhookDispatchFailed(_logger, webhook.Id, ex);
        }
    }

    public async Task DispatchToWebhookAsync(WebhookSubscription webhook, string eventType, object payload, CancellationToken ct = default)
    {
        using var client = _httpClientFactory.CreateClient("WebhookClient");
        client.Timeout = TimeSpan.FromSeconds(10);

        var payloadJson = JsonSerializer.Serialize(new
        {
            @event = eventType,
            projectId = webhook.ProjectId,
            timestamp = DateTimeOffset.UtcNow,
            data = payload
        });

        var request = new HttpRequestMessage(HttpMethod.Post, webhook.PayloadUrl);
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        
        // Compute signature if secret is provided
        if (!string.IsNullOrEmpty(webhook.Secret))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhook.Secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            var signature = "sha256=" + Convert.ToHexStringLower(hash);
            request.Headers.Add("X-Qaly-Signature", signature);
        }
        request.Headers.Add("X-Qaly-Event", eventType);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? responseBody = null;

        try
        {
            response = await client.SendAsync(request, ct);
            responseBody = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            responseBody = ex.Message;
        }
        finally
        {
            stopwatch.Stop();

            var log = new WebhookDeliveryLog
            {
                WebhookId = webhook.Id,
                EventType = eventType,
                RequestPayload = payloadJson,
                ResponseStatusCode = response != null ? (int)response.StatusCode : null,
                ResponseBody = responseBody,
                DurationMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = response?.IsSuccessStatusCode ?? false
            };

            if (!log.IsSuccess)
            {
                webhook.FailureCount++;
                if (webhook.FailureCount >= 10) // Disable after 10 consecutive failures
                {
                    webhook.IsActive = false;
                }
                await _webhookRepo.UpdateAsync(webhook, ct);
            }
            else
            {
                if (webhook.FailureCount > 0)
                {
                    webhook.FailureCount = 0;
                    await _webhookRepo.UpdateAsync(webhook, ct);
                }
            }

            await _logRepo.AddAsync(log, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to dispatch webhook {WebhookId}")]
    private static partial void LogWebhookDispatchFailed(ILogger logger, Guid webhookId, Exception ex);

    private static string[] DeserializeEvents(string? eventsJson)
    {
        if (string.IsNullOrWhiteSpace(eventsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(eventsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
