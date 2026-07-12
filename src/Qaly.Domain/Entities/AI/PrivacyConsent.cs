using System;

namespace Qaly.Domain.Entities;

public class PrivacyConsent : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public string ConsentType { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public string SourceType { get; set; } = "meeting";
    public Guid? SourceEntityId { get; set; }
    public string ProviderClass { get; set; } = PrivacyProviderClasses.Local;
    public string PolicyVersion { get; set; } = string.Empty;
    public string NoticeVersion { get; set; } = string.Empty;
    public Guid? RetentionPolicyId { get; set; }
    public string? ScopeJson { get; set; }
    public string? EvidenceJson { get; set; }
    public string Status { get; set; } = PrivacyConsentStatuses.Granted;
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? GrantedById { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedById { get; set; }
    public DateTimeOffset? DeniedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? RequestId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public RetentionPolicy? RetentionPolicy { get; set; }
}
