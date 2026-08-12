namespace Qaly.Domain.Entities;

public class AiJobActivityEvent : BaseEntity
{
    public Guid AiJobId { get; set; }
    public int Sequence { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PublicLabel { get; set; } = string.Empty;
    public string? SafeDetailJson { get; set; }
    public int? Current { get; set; }
    public int? Total { get; set; }
    public int Attempt { get; set; } = 1;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int? DurationMs { get; set; }
    public bool Retryable { get; set; }
    public string? ReceiptLink { get; set; }

    public AiJob AiJob { get; set; } = null!;
}

