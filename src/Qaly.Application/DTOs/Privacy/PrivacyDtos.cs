namespace Qaly.Application.DTOs.Privacy;

public record RetentionPolicyUpsertRequest(
    Guid TenantId,
    Guid? ProjectId,
    string Name,
    string DataClassification,
    string Purpose,
    IReadOnlyList<int> AllowedRetentionDays,
    int DefaultRetentionDays,
    string ExpiryAction,
    bool AllowCloudProcessing,
    bool AllowLocalProcessing,
    bool RequireExplicitConsent,
    Guid? ApprovalOwnerUserId,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveUntil);

public record RetentionPolicyDto(
    Guid Id,
    Guid TenantId,
    Guid? ProjectId,
    string Name,
    string DataClassification,
    string Purpose,
    IReadOnlyList<int> AllowedRetentionDays,
    int DefaultRetentionDays,
    string ExpiryAction,
    bool AllowCloudProcessing,
    bool AllowLocalProcessing,
    bool RequireExplicitConsent,
    bool IsActive,
    string PolicyVersion,
    Guid? ApprovalOwnerUserId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    string RowVersion);

public record RetentionPolicyDisableRequest(string Reason, string RowVersion);

public record PrivacyConsentGrantRequest(
    Guid ProjectId,
    Guid RetentionPolicyId,
    string Purpose,
    string ProviderClass,
    string SourceType,
    Guid? SourceEntityId,
    string NoticeVersion,
    DateTimeOffset? ExpiresAt);

public record PrivacyConsentRevokeRequest(string Reason, string RowVersion);

public record PrivacyConsentDto(
    Guid Id,
    Guid? TenantId,
    Guid? ProjectId,
    Guid? UserId,
    string ConsentType,
    string Purpose,
    string SourceType,
    Guid? SourceEntityId,
    string ProviderClass,
    string PolicyVersion,
    string NoticeVersion,
    Guid? RetentionPolicyId,
    string Status,
    DateTimeOffset GrantedAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? ExpiresAt,
    string RowVersion);

public record PrivacyDecisionRequest(
    Guid ProjectId,
    Guid RetentionPolicyId,
    Guid? ConsentId,
    string Purpose,
    string DataClassification,
    string ProviderClass,
    int? RetentionDays,
    string SourceType,
    Guid? SourceEntityId);

public record PrivacyDecisionDto(
    bool Allowed,
    bool CloudEligible,
    bool LocalEligible,
    string? ErrorCode,
    string Reason,
    Guid? RetentionPolicyId,
    string? PolicyVersion,
    Guid? ConsentId,
    int? RetentionDays,
    DateTimeOffset? RetentionExpiresAt,
    string? ExpiryAction,
    string ProviderClass,
    DateTimeOffset EvaluatedAt);

public record DataSubjectRequestSubmitRequest(
    Guid TenantId,
    Guid? ProjectId,
    Guid? SubjectUserId,
    string RequestType,
    string Scope,
    string IdempotencyKey);

public record DataSubjectRequestDecisionRequest(string? Reason);

public record DataSubjectRequestDto(
    Guid Id,
    Guid? TenantId,
    Guid? ProjectId,
    Guid? RequesterUserId,
    Guid? SubjectUserId,
    string RequestType,
    string Scope,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? IdentityVerifiedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? DeadlineAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ResultExpiresAt,
    bool LegalHoldDetected,
    string? LegalHoldReason,
    string? RejectionReason,
    int AttemptCount,
    string? LastErrorCode,
    string RowVersion);

public sealed record PrivacyExportArtifact(
    byte[] Content,
    string ContentType,
    string FileName,
    DateTimeOffset ExpiresAt);

public record PrivacyLegalHoldCreateRequest(
    Guid TenantId,
    Guid? ProjectId,
    Guid? SubjectUserId,
    string? EntityType,
    Guid? EntityId,
    string Reason);

public record PrivacyLegalHoldReleaseRequest(string Reason, string RowVersion);

public record PrivacyLegalHoldDto(
    Guid Id,
    Guid TenantId,
    Guid? ProjectId,
    Guid? SubjectUserId,
    string? EntityType,
    Guid? EntityId,
    string Status,
    string Reason,
    DateTimeOffset HeldAt,
    DateTimeOffset? ReleasedAt,
    string RowVersion);

public record PrivacyHealthDto(
    bool Enabled,
    bool EnforcementEnabled,
    bool WorkerEnabled,
    string Status,
    long PendingRetentionActions,
    long FailedRetentionActions,
    long PendingDataSubjectRequests,
    long FailedDataSubjectRequests,
    DateTimeOffset? OldestAvailableWorkAt);

public record PrivacyWorkerRunResultDto(
    bool WorkFound,
    string? Kind,
    Guid? WorkId,
    string Outcome);
