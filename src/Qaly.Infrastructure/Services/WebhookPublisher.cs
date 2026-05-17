using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
