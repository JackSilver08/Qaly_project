using Qaly.Domain.Entities;

namespace Qaly.Application.Common.Interfaces;

public interface IWebhookPublisher
{
    Task PublishAsync(Guid projectId, string eventType, object payload, CancellationToken ct = default);
    Task<WebhookPublishReceipt> PublishOutboxAsync(
        Guid outboxMessageId,
        Guid projectId,
        string eventType,
        object payload,
        CancellationToken ct = default);
    Task<WebhookDispatchReceipt> DispatchToWebhookAsync(
        Guid webhookId,
        string eventType,
        object payload,
        CancellationToken ct = default);
}

public sealed record WebhookPublishReceipt(
    Guid OutboxMessageId,
    int SubscriptionCount,
    int DeliveredCount,
    IReadOnlyList<string> FailedStatuses)
{
    public bool IsComplete => FailedStatuses.Count == 0;
}

public sealed record WebhookDispatchReceipt(
    Guid WebhookId,
    string EventType,
    string IdempotencyKey,
    string Status,
    bool IsDelivered,
    int AttemptCount,
    int? ResponseStatusCode,
    Guid? DeliveryLogId);
