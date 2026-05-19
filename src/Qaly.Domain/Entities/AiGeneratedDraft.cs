namespace Qaly.Domain.Entities;

public class AiGeneratedDraft : BaseEntity
{
    public Guid AiJobId { get; set; }
    public Guid ProjectId { get; set; }
    public string DraftType { get; set; } = "TaskDraft";
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public Guid? ConfirmedById { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmAction { get; set; }
    public string? ConfirmationNote { get; set; }

    public AiJob AiJob { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public User? ConfirmedBy { get; set; }
}
