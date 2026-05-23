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

    public WebhookPublisher(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync(Guid projectId, string eventType, object payload, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var webhookRepo = scope.ServiceProvider.GetRequiredService<IRepository<WebhookSubscription>>();

        var webhooks = await webhookRepo.GetQueryable()
            .Where(w => w.ProjectId == projectId && w.IsActive)
            .ToListAsync(ct);

        foreach (var webhook in webhooks)
        {
            var events = DeserializeEvents(webhook.Events);
            if (events.Contains(eventType) || events.Contains("*"))
            {
                // Dispatch in background with a NEW scope to avoid using the request scope
                _ = Task.Run(async () => await DispatchToWebhookSafeAsync(webhook.Id, eventType, payload), CancellationToken.None);
            }
        }
    }

    private async Task DispatchToWebhookSafeAsync(Guid webhookId, string eventType, object payload)
    {
        try
        {
            await DispatchToWebhookAsync(webhookId, eventType, payload);
        }
        catch (Exception ex)
        {
            LogWebhookDispatchFailed(_logger, webhookId, ex);
        }
    }

    public async Task DispatchToWebhookAsync(Guid webhookId, string eventType, object payload, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var webhookRepo = scope.ServiceProvider.GetRequiredService<IRepository<WebhookSubscription>>();
        var logRepo = scope.ServiceProvider.GetRequiredService<IRepository<WebhookDeliveryLog>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var webhook = await webhookRepo.GetByIdAsync(webhookId, ct);
        if (webhook == null) return;

        using var client = _httpClientFactory.CreateClient("WebhookClient");
        client.Timeout = TimeSpan.FromSeconds(10);

        var dataJson = JsonSerializer.Serialize(payload);
        var idempotencyKey = BuildIdempotencyKey(webhook.Id, eventType, dataJson);
        var alreadyDelivered = await logRepo.GetQueryable()
            .AsNoTracking()
            .AnyAsync(log =>
                log.WebhookId == webhook.Id &&
                log.IdempotencyKey == idempotencyKey &&
                log.IsSuccess, ct);

        if (alreadyDelivered)
        {
            LogSkippedDuplicateWebhook(_logger, eventType, webhook.Id, idempotencyKey);
            return;
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

    private static string BuildIdempotencyKey(Guid webhookId, string eventType, string dataJson)
    {
        var seed = $"{webhookId}|{eventType}|{dataJson}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seed)));
        return $"webhook:{hash}";
    }
}
