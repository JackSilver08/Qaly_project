namespace Qaly.Domain.Entities;

public class MeetingImport : BaseEntity
{
    public Guid? TenantId { get; set; }
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
    public string DataClassification { get; set; } = PrivacyDataClasses.SensitiveCollaboration;
    public string PrivacyState { get; set; } = MeetingPrivacyStates.Active;
    public string ProcessingPurpose { get; set; } = PrivacyPurposes.MeetingActionExtraction;
    public string ProviderClass { get; set; } = PrivacyProviderClasses.Local;
    public Guid? ConsentId { get; set; }
    public Guid? RetentionPolicyId { get; set; }
    public string? PolicyVersion { get; set; }
    public DateTimeOffset? RetentionExpiresAt { get; set; }
    public DateTimeOffset? ContentRedactedAt { get; set; }
    public DateTimeOffset? ContentDeletedAt { get; set; }
    public Guid? AiJobId { get; set; }
    public Guid? AiDraftId { get; set; }

    public Project Project { get; set; } = null!;
    public User ImportedBy { get; set; } = null!;
    public AiJob? AiJob { get; set; }
    public AiGeneratedDraft? AiDraft { get; set; }
    public PrivacyConsent? Consent { get; set; }
    public RetentionPolicy? RetentionPolicy { get; set; }
    public ICollection<MeetingActionItemMapping> ActionItemMappings { get; set; } = new List<MeetingActionItemMapping>();
}
