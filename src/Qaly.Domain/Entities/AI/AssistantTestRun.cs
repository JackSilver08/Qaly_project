namespace Qaly.Domain.Entities;

public sealed class AssistantTestRun : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid SessionId { get; set; }
    public Guid OriginTurnId { get; set; }
    public string ManifestId { get; set; } = string.Empty;
    public string Status { get; set; } = "review_required";
    public string? IdempotencyKey { get; set; }
    public long Revision { get; set; }
    public string EventsJson { get; set; } = "[]";
    public string? SafeSummary { get; set; }
    public string? SafeErrorCode { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
