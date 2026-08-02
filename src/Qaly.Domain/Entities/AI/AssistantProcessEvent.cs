namespace Qaly.Domain.Entities;

public sealed class AssistantProcessEvent : BaseEntity
{
    public Guid TurnId { get; set; }
    public int Sequence { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PublicLabel { get; set; } = string.Empty;
    public string? SafeDetailJson { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int? DurationMs { get; set; }
    public bool Retryable { get; set; }
    public string? SafeErrorCode { get; set; }

    public AssistantTurn Turn { get; set; } = null!;
}
