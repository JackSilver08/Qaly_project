namespace Qaly.Domain.Entities;

public class AiGeneratedDraft : BaseEntity
{
    public Guid AiJobId { get; set; }
    public Guid ProjectId { get; set; }
    public string DraftType { get; set; } = "TaskDraft";
    public string PayloadJson { get; set; } = "{}";
    public string OriginalPayloadJson { get; set; } = "{}";
    public string WorkingPayloadJson { get; set; } = "{}";
    public string? WarningsJson { get; set; }
    public string Status { get; set; } = AiDraftStatuses.PendingReview;
    public Guid? ConfirmedById { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? ConfirmAction { get; set; }
    public string? ConfirmationNote { get; set; }
    public string? ConfirmationIdempotencyKey { get; set; }
    public string? ConfirmationResultJson { get; set; }
    public string? SchemaId { get; set; }
    public decimal? Confidence { get; set; }
    public string? SourceHashAtGeneration { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public AiJob AiJob { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public User? ConfirmedBy { get; set; }
    public User? RejectedBy { get; set; }
}
