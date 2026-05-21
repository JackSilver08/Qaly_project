namespace Qaly.Domain.Entities;

public class MeetingImport : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid ImportedById { get; set; }
    public string SourceProvider { get; set; } = "meetily";
    public string SourceId { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset? MeetingStartedAt { get; set; }
    public string? Summary { get; set; }
    public string TranscriptText { get; set; } = string.Empty;
    public string ParticipantsJson { get; set; } = "[]";
    public string RawPayloadJson { get; set; } = "{}";
    public Guid? AiJobId { get; set; }
    public Guid? AiDraftId { get; set; }

    public Project Project { get; set; } = null!;
    public User ImportedBy { get; set; } = null!;
    public AiJob? AiJob { get; set; }
    public AiGeneratedDraft? AiDraft { get; set; }
}
