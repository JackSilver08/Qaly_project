namespace Qaly.Domain.Entities;

public sealed class AssistantTurn : BaseEntity
{
    public Guid SessionId { get; set; }
    public int Sequence { get; set; }
    public Guid ClientTurnId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public string? RequestContextJson { get; set; }
    public string? RequestPayloadJson { get; set; }
    public string Status { get; set; } = "running";
    public string? Disposition { get; set; }
    public string? Intent { get; set; }
    public string? ExecutionPolicy { get; set; }
    public string? AssistantResponse { get; set; }
    public string? ResponseJson { get; set; }
    public string? SourceRefsJson { get; set; }
    public string ModelProfile { get; set; } = "auto";
    public string? ActualProvider { get; set; }
    public string? ActualModel { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? SafeErrorCode { get; set; }
    public DateTimeOffset? CancellationRequestedAt { get; set; }
    public Guid? ResumedFromTurnId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public AssistantSession Session { get; set; } = null!;
    public ICollection<AssistantProcessEvent> ProcessEvents { get; set; } = new List<AssistantProcessEvent>();
    public ICollection<AssistantArtifactRef> ArtifactRefs { get; set; } = new List<AssistantArtifactRef>();
}
