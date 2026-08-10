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
