namespace Qaly.Application.Common.Models;

public static class PrivacyErrorCodes
{
    public const string Disabled = "PRIVACY_V4_DISABLED";
    public const string TenantRequired = "PRIVACY_TENANT_REQUIRED";
    public const string PolicyRequired = "PRIVACY_POLICY_REQUIRED";
    public const string PolicyInactive = "PRIVACY_POLICY_INACTIVE";
    public const string PolicyMismatch = "PRIVACY_POLICY_MISMATCH";
    public const string PurposeMismatch = "PRIVACY_PURPOSE_MISMATCH";
    public const string ConsentRequired = "PRIVACY_CONSENT_REQUIRED";
    public const string ConsentInvalid = "PRIVACY_CONSENT_INVALID";
    public const string ConsentRevoked = "PRIVACY_CONSENT_REVOKED";
    public const string ConsentExpired = "PRIVACY_CONSENT_EXPIRED";
    public const string RetentionUnsupported = "PRIVACY_RETENTION_UNSUPPORTED";
    public const string CloudBlocked = "PRIVACY_CLOUD_BLOCKED";
    public const string LocalBlocked = "PRIVACY_LOCAL_BLOCKED";
    public const string PayloadTooLarge = "PRIVACY_PAYLOAD_TOO_LARGE";
    public const string LegalHold = "PRIVACY_LEGAL_HOLD";
    public const string IdentityRequired = "PRIVACY_IDENTITY_REQUIRED";
    public const string ExportExpired = "PRIVACY_EXPORT_EXPIRED";
    public const string InvalidTransition = "PRIVACY_INVALID_TRANSITION";
    public const string IdempotencyConflict = "PRIVACY_IDEMPOTENCY_CONFLICT";
    public const string WorkerPaused = "PRIVACY_WORKER_PAUSED";
    public const string WorkerUnavailable = "PRIVACY_WORKER_UNAVAILABLE";
}

public sealed record PrivacyProcessingRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public Guid UserId { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string DataClassification { get; init; } = string.Empty;
    public string ProviderClass { get; init; } = string.Empty;
    public Guid? ConsentId { get; init; }
    public Guid? RetentionPolicyId { get; init; }
    public int? RetentionDays { get; init; }
    public string? NoticeVersion { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public Guid? SourceEntityId { get; init; }
}

public sealed record PrivacyProcessingDecision(
    bool Allowed,
    bool CloudEligible,
    bool LocalEligible,
    string? ErrorCode,
    string Reason,
    Guid TenantId,
    Guid ProjectId,
    Guid? RetentionPolicyId,
    string? PolicyVersion,
    Guid? ConsentId,
    int? RetentionDays,
    DateTimeOffset? RetentionExpiresAt,
    string? ExpiryAction,
    string ProviderClass,
    DateTimeOffset EvaluatedAt)
{
    public static PrivacyProcessingDecision Blocked(
        PrivacyProcessingRequest request,
        string errorCode,
        string reason,
        DateTimeOffset evaluatedAt,
        Guid? policyId = null,
        string? policyVersion = null,
        Guid? consentId = null)
        => new(
            false,
            false,
            false,
            errorCode,
            reason,
            request.TenantId,
            request.ProjectId,
            policyId,
            policyVersion,
            consentId,
            null,
            null,
            null,
            request.ProviderClass,
            evaluatedAt);
}

public sealed class PrivacyAuditRecord
{
    public Guid? TenantId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public Guid? AiJobId { get; init; }
    public Guid? ProviderAttemptId { get; init; }
    public Guid? PrivacyConsentId { get; init; }
    public Guid? RetentionPolicyId { get; init; }
    public Guid? DataSubjectRequestId { get; init; }
    public string? Purpose { get; init; }
    public string? PolicyVersion { get; init; }
    public string? DataClassification { get; init; }
    public string? ProviderClass { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public string? FailureCode { get; init; }
    public string? RequestId { get; init; }
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>();
}
