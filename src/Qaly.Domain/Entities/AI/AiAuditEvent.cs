using System;

namespace Qaly.Domain.Entities;

public class AiAuditEvent : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string EventType { get; set; } = null!;
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }
    public Guid? EntityGuid { get; set; }
    public string? EntityKey { get; set; }
    public long? JobId { get; set; }
    public Guid? AiJobId { get; set; }
    public Guid? ProviderAttemptId { get; set; }
    public Guid? PrivacyConsentId { get; set; }
    public Guid? RetentionPolicyId { get; set; }
    public Guid? DataSubjectRequestId { get; set; }
    public string? Purpose { get; set; }
    public string? PolicyVersion { get; set; }
    public string? DataClassification { get; set; }
    public string? ProviderClass { get; set; }
    public string? Outcome { get; set; }
    public string? FailureCode { get; set; }
    public string? RequestId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
