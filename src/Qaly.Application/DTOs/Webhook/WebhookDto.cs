namespace Qaly.Application.DTOs.Webhook;

public sealed record WebhookDto(
    Guid Id,
    Guid ProjectId,
    string PayloadUrl,
    string[] Events,
    bool HasSecret,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public sealed record CreateWebhookDto(
    Guid ProjectId,
    string PayloadUrl,
    string Secret,
    string[] Events
);

public sealed record UpdateWebhookDto(
    string PayloadUrl,
    string Secret,
    string[] Events,
    bool IsActive
);

public sealed record WebhookTestResultDto(
    Guid WebhookId,
    string EventType,
    string DeliveryStatus,
    bool IsDelivered,
    int AttemptCount,
    int? ResponseStatusCode,
    Guid? DeliveryLogId,
    string IdempotencyKey);

public sealed record WebhookOutboxItemDto(
    Guid Id,
    Guid ProjectId,
    string EventType,
    string Status,
    int RetryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? DeadLetteredAt,
    string? ErrorSummary);

public sealed record WebhookDeliveryLogDto(
    Guid Id,
    Guid WebhookId,
    string EventType,
    bool IsSuccess,
    int AttemptCount,
    int? ResponseStatusCode,
    long DurationMs,
    DateTimeOffset CreatedAt);

public sealed record WebhookOperationsDto(
    Guid ProjectId,
    int PendingCount,
    int DeadLetterCount,
    IReadOnlyList<WebhookOutboxItemDto> RecentOutbox,
    IReadOnlyList<WebhookDeliveryLogDto> RecentDeliveries);

public sealed record WebhookOutboxReplayResultDto(
    WebhookOutboxItemDto Item,
    bool ReplayQueued);
