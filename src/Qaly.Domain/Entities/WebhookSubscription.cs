namespace Qaly.Domain.Entities;

public class WebhookSubscription : BaseEntity, ISoftDeleteEntity
{
    public Guid ProjectId { get; set; }
    public string PayloadUrl { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string Events { get; set; } = "[]"; // JSON array of subscribed events
    public bool IsActive { get; set; } = true;
    public int FailureCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Project Project { get; set; } = null!;
}
