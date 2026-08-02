namespace Qaly.Domain.Entities;

/// <summary>
/// An explicit, manager-confirmed statement that a named contributor completed
/// work carrying a task's confirmed skill requirements. It is intentionally not
/// inferred from assignment history.
/// </summary>
public sealed class TaskCompletionAttribution : BaseEntity
{
    public const string Confirmed = "Confirmed";
    public const string CorrectionRequested = "CorrectionRequested";
    public const string Revoked = "Revoked";

    public Guid TaskItemId { get; set; }
    public Guid ContributorUserId { get; set; }
    public Guid ConfirmedByUserId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = Confirmed;
    public string AttributionPolicyVersion { get; set; } = "completion-contributor.v1";
    public string? CorrectionReason { get; set; }
    public DateTimeOffset? CorrectionRequestedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public TaskItem TaskItem { get; set; } = null!;
    public User ContributorUser { get; set; } = null!;
    public User ConfirmedByUser { get; set; } = null!;
}
