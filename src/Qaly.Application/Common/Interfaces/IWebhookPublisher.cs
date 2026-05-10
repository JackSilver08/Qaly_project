using Qaly.Domain.Entities;

namespace Qaly.Application.Common.Interfaces;

public interface IWebhookPublisher
{
    Task PublishAsync(Guid projectId, string eventType, object payload, CancellationToken ct = default);
    Task DispatchToWebhookAsync(WebhookSubscription webhook, string eventType, object payload, CancellationToken ct = default);
}
