using System;

namespace Qaly.Domain.Entities;

public class PrivacyConsent : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public string ConsentType { get; set; } = null!; // meeting_import|ai_cloud_processing|transcript_storage
    public string Purpose { get; set; } = null!;
    public string? ScopeJson { get; set; }
    public string Status { get; set; } = "granted"; // granted|revoked|expired
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}