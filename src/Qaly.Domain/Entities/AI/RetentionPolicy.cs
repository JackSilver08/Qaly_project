namespace Qaly.Domain.Entities;

public class RetentionPolicy : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataClassification { get; set; } = PrivacyDataClasses.SensitiveCollaboration;
    public string Purpose { get; set; } = PrivacyPurposes.MeetingActionExtraction;
    public string AllowedRetentionDaysJson { get; set; } = "[30]";
    public int DefaultRetentionDays { get; set; } = 30;
    public string ExpiryAction { get; set; } = PrivacyExpiryActions.Redact;
    public string LegalHoldBehavior { get; set; } = PrivacyLegalHoldBehaviors.PauseAndReview;
    public bool AllowCloudProcessing { get; set; }
    public bool AllowLocalProcessing { get; set; } = true;
    public bool RequireExplicitConsent { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string PolicyVersion { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
    public Guid? ApprovalOwnerUserId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveUntil { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Project? Project { get; set; }
    public ICollection<PrivacyConsent> Consents { get; set; } = new List<PrivacyConsent>();
    public ICollection<PrivacyRetentionAction> RetentionActions { get; set; } = new List<PrivacyRetentionAction>();
}
