namespace Qaly.Domain.Entities;

public class WebhookDeliveryLog : BaseEntity
{
    public Guid WebhookId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RequestPayload { get; set; } = string.Empty;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public long DurationMs { get; set; }
    public bool IsSuccess { get; set; }

    public WebhookSubscription Webhook { get; set; } = null!;
}
