namespace Qaly.Domain.Entities;

public sealed class AssistantArtifactRef : BaseEntity
{
    public Guid TurnId { get; set; }
    public string SchemaId { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "v1";
    public Guid? AiJobId { get; set; }
    public Guid? DraftId { get; set; }
    public Guid? ReceiptId { get; set; }
    public string RendererId { get; set; } = string.Empty;

    public AssistantTurn Turn { get; set; } = null!;
}
