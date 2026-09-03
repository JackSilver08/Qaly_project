using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qaly.Infrastructure.Services;

public partial class WebhookPublisher : IWebhookPublisher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookPublisher> _logger;
    private readonly IWebhookEndpointPolicy _webhookEndpointPolicy;

    public WebhookPublisher(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookPublisher> logger,
        IWebhookEndpointPolicy webhookEndpointPolicy)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _webhookEndpointPolicy = webhookEndpointPolicy;
    }

    public async Task PublishAsync(Guid projectId, string eventType, object payload, CancellationToken ct = default)
    {
        var webhooks = await GetMatchingWebhooksAsync(projectId, eventType, ct);
        var dispatches = webhooks
            .Select(webhook => DispatchToWebhookSafeAsync(webhook.Id, eventType, payload, ct));

        // The caller receives a result only after each delivery has reached a canonical log state.
        // This avoids process-loss windows from detached Task.Run work while each dispatch still
        // owns a fresh DI scope.
        await Task.WhenAll(dispatches);
    }

    public async Task<WebhookPublishReceipt> PublishOutboxAsync(
        Guid outboxMessageId,
        Guid projectId,
        string eventType,
        object payload,
        CancellationToken ct = default)
    {
        var webhooks = await GetMatchingWebhooksAsync(projectId, eventType, ct);
        if (webhooks.Count == 0)
        {
            return new WebhookPublishReceipt(outboxMessageId, 0, 0, []);
        }

        var occurrenceKey = $"outbox:{outboxMessageId:N}";
        var receipts = await Task.WhenAll(webhooks.Select(webhook =>
            DispatchToWebhookAsync(webhook.Id, eventType, payload, occurrenceKey, ct)));
        var failedStatuses = receipts
            .Where(receipt => !receipt.IsDelivered)
            .Select(receipt => $"{receipt.WebhookId:N}:{receipt.Status}")
            .ToArray();

        return new WebhookPublishReceipt(
            outboxMessageId,
            receipts.Length,
            receipts.Count(receipt => receipt.IsDelivered),
            failedStatuses);
    }

    private async Task<List<WebhookSubscription>> GetMatchingWebhooksAsync(
        Guid projectId,
        string eventType,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var webhookRepo = scope.ServiceProvider.GetRequiredService<IRepository<WebhookSubscription>>();
        var webhooks = await webhookRepo.GetQueryable()
            .AsNoTracking()
            .Where(webhook => webhook.ProjectId == projectId && webhook.IsActive)
            .OrderBy(webhook => webhook.CreatedAt)
            .ThenBy(webhook => webhook.Id)
            .ToListAsync(ct);

        return webhooks
            .Where(webhook =>
            {
                var events = DeserializeEvents(webhook.Events);
                return events.Contains(eventType) || events.Contains("*");
            })
            .ToList();
    }

    private async Task DispatchToWebhookSafeAsync(
        Guid webhookId,
        string eventType,
        object payload,
        CancellationToken ct)
    {
        try
        {
            await DispatchToWebhookAsync(webhookId, eventType, payload, ct);
        }
        catch (Exception ex)
        {
            LogWebhookDispatchFailed(_logger, webhookId, ex);
        }
    }

    public async Task<WebhookDispatchReceipt> DispatchToWebhookAsync(
        Guid webhookId,
        string eventType,
        object payload,
        CancellationToken ct = default)
        => await DispatchToWebhookAsync(webhookId, eventType, payload, null, ct);

    private async Task<WebhookDispatchReceipt> DispatchToWebhookAsync(
        Guid webhookId,
        string eventType,
        object payload,
        string? occurrenceKey,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var webhookRepo = scope.ServiceProvider.GetRequiredService<IRepository<WebhookSubscription>>();
        var logRepo = scope.ServiceProvider.GetRequiredService<IRepository<WebhookDeliveryLog>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var webhook = await webhookRepo.GetByIdAsync(webhookId, ct);
        if (webhook == null)
        {
            return new WebhookDispatchReceipt(
                webhookId,
                eventType,
                string.Empty,
                "not_found",
                false,
                0,
                null,
                null);
        }

        var dataJson = JsonSerializer.Serialize(payload);
        var idempotencyKey = BuildIdempotencyKey(webhook.Id, eventType, dataJson, occurrenceKey);
        var alreadyDelivered = await logRepo.GetQueryable()
            .AsNoTracking()
            .Where(log =>
                log.WebhookId == webhook.Id &&
                log.IdempotencyKey == idempotencyKey &&
                log.IsSuccess)
            .OrderByDescending(log => log.CreatedAt)
            .ThenBy(log => log.Id)
            .FirstOrDefaultAsync(ct);

        if (alreadyDelivered != null)
        {
            LogSkippedDuplicateWebhook(_logger, eventType, webhook.Id, idempotencyKey);
            return new WebhookDispatchReceipt(
                webhook.Id,
                eventType,
                idempotencyKey,
                "already_delivered",
                true,
                alreadyDelivered.AttemptCount,
                alreadyDelivered.ResponseStatusCode,
                alreadyDelivered.Id);
        }

        var payloadJson = JsonSerializer.Serialize(new
        {
            idempotencyKey,
            @event = eventType,
            projectId = webhook.ProjectId,
            timestamp = DateTimeOffset.UtcNow,
            data = payload
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int? responseStatusCode = null;
        string? responseBody = null;
        var isSuccess = false;
        var attempt = 0;

        var endpointValidation = await _webhookEndpointPolicy.ValidateAsync(webhook.PayloadUrl, ct);
        if (!endpointValidation.IsAllowed)
        {
            attempt = 1;
            responseBody = endpointValidation.Error;
        }
        else
        {
            using var client = _httpClientFactory.CreateClient("WebhookClient");
            client.Timeout = TimeSpan.FromSeconds(10);

            for (attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var request = BuildRequest(webhook, eventType, idempotencyKey, payloadJson);
                    using var response = await client.SendAsync(request, ct);
                    responseStatusCode = (int)response.StatusCode;
                    responseBody = await response.Content.ReadAsStringAsync(ct);
                    isSuccess = response.IsSuccessStatusCode;

                    if (isSuccess || !ShouldRetry(response.StatusCode))
                    {
                        break;
                    }
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    responseStatusCode = null;
                    responseBody = ex.Message;
                }

                if (attempt < 3)
                {
                    LogWebhookDeliveryFailedRetry(_logger, eventType, webhook.Id, attempt, idempotencyKey);
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct);
                }
            }
        }

        stopwatch.Stop();

        var log = new WebhookDeliveryLog
        {
            WebhookId = webhook.Id,
            EventType = eventType,
            IdempotencyKey = idempotencyKey,
            RequestPayload = payloadJson,
            ResponseStatusCode = responseStatusCode,
            ResponseBody = responseBody,
            DurationMs = stopwatch.ElapsedMilliseconds,
            AttemptCount = Math.Min(attempt, 3),
            IsSuccess = isSuccess
        };

        if (!log.IsSuccess)
        {
            webhook.FailureCount++;
            if (webhook.FailureCount >= 10) // Disable after 10 consecutive failures
            {
                webhook.IsActive = false;
            }
            await webhookRepo.UpdateAsync(webhook, ct);
        }
        else
        {
            if (webhook.FailureCount > 0)
            {
                webhook.FailureCount = 0;
                await webhookRepo.UpdateAsync(webhook, ct);
            }
        }

        await logRepo.AddAsync(log, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new WebhookDispatchReceipt(
            webhook.Id,
            eventType,
            idempotencyKey,
            isSuccess ? "delivered" : endpointValidation.IsAllowed ? "delivery_failed" : "endpoint_blocked",
            isSuccess,
            log.AttemptCount,
            responseStatusCode,
            log.Id);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to dispatch webhook {WebhookId}")]
    private static partial void LogWebhookDispatchFailed(ILogger logger, Guid webhookId, Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Skipped duplicate webhook delivery {EventType} to {WebhookId} with key {IdempotencyKey}.")]
    private static partial void LogSkippedDuplicateWebhook(ILogger logger, string eventType, Guid webhookId, string idempotencyKey);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Webhook delivery {EventType} to {WebhookId} failed on attempt {Attempt}; retrying. IdempotencyKey={IdempotencyKey}")]
    private static partial void LogWebhookDeliveryFailedRetry(ILogger logger, string eventType, Guid webhookId, int attempt, string idempotencyKey);

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

    private static HttpRequestMessage BuildRequest(
        WebhookSubscription webhook,
        string eventType,
        string idempotencyKey,
        string payloadJson)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, webhook.PayloadUrl);
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        if (!string.IsNullOrEmpty(webhook.Secret))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhook.Secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            var signature = "sha256=" + Convert.ToHexStringLower(hash);
            request.Headers.Add("X-Qaly-Signature", signature);
        }

        request.Headers.Add("X-Qaly-Event", eventType);
        request.Headers.Add("X-Qaly-Idempotency-Key", idempotencyKey);
        return request;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout ||
           statusCode == HttpStatusCode.TooManyRequests ||
           (int)statusCode >= 500;

    private static string BuildIdempotencyKey(
        Guid webhookId,
        string eventType,
        string dataJson,
        string? occurrenceKey = null)
    {
        var seed = $"{webhookId}|{eventType}|{occurrenceKey ?? dataJson}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seed)));
        return $"webhook:{hash}";
    }
}
