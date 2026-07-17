namespace Qaly.Domain.Entities;

public static class PrivacyDataClasses
{
    public const string Public = "public";
    public const string Internal = "internal";
    public const string Confidential = "confidential";
    public const string SensitiveCollaboration = "sensitive_collaboration";
    public const string Secret = "secret";
    public const string UnknownSensitive = "unknown_sensitive";

    public static bool IsSensitive(string? value)
        => value is SensitiveCollaboration or Secret or UnknownSensitive;
}

public static class PrivacyConsentStatuses
{
    public const string Granted = "granted";
    public const string Revoked = "revoked";
    public const string Expired = "expired";
    public const string Denied = "denied";
}

public static class PrivacyProviderClasses
{
    public const string Local = "local";
    public const string Cloud = "cloud";
    public const string Any = "any";
    public const string Unknown = "unknown";
}

public static class PrivacyPurposes
{
    public const string MeetingCapture = "meeting_capture";
    public const string MeetingImport = "meeting_import";
    public const string MeetingActionExtraction = "meeting_action_extraction";
    public const string AiCloudProcessing = "ai_cloud_processing";
    public const string SubjectExport = "subject_export";
    public const string SubjectDeletion = "subject_deletion";
}

public static class PrivacyExpiryActions
{
    public const string Redact = "redact";
    public const string Delete = "delete";
    public const string Review = "review";
}

public static class PrivacyLegalHoldBehaviors
{
    public const string PauseAndReview = "pause_and_review";
}

public static class MeetingPrivacyStates
{
    public const string Active = "active";
    public const string MigrationReview = "migration_review";
    public const string Expired = "expired";
    public const string Redacted = "redacted";
    public const string Deleted = "deleted";
    public const string LegalHold = "legal_hold";
}

public static class PrivacyProcessingModes
{
    public const string LocalOnly = "local_only";
    public const string CloudAllowed = "cloud_allowed";
}

public static class DataSubjectRequestStatuses
{
    public const string Submitted = "submitted";
    public const string IdentityVerification = "identity_verification";
    public const string Accepted = "accepted";
    public const string Collecting = "collecting";
    public const string ReviewRequired = "review_required";
    public const string Completed = "completed";
    public const string PartiallyCompleted = "partially_completed";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
}

public static class DataSubjectRequestTypes
{
    public const string Export = "export";
    public const string Delete = "delete";
}

public static class PrivacyWorkerStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string LegalHold = "legal_hold";
}

public static class PrivacyLegalHoldStatuses
{
    public const string Active = "active";
    public const string Released = "released";
}
