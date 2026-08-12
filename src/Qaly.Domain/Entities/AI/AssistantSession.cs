namespace Qaly.Domain.Entities;

public sealed class AssistantSession : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "Cuộc trò chuyện mới";
    public string Status { get; set; } = "active";
    public long Version { get; set; }
    public int LastSequence { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ClarificationDraftJson { get; set; }
    public ICollection<AssistantTurn> Turns { get; set; } = new List<AssistantTurn>();
}
