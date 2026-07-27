namespace Qaly.Application.Common.Models;

public static class AiErrorCodes
{
    public const string JobNotFound = "AI_JOB_NOT_FOUND";
    public const string PermissionDenied = "AI_PERMISSION_DENIED";
    public const string SensitiveBlocked = "AI_SENSITIVE_BLOCKED";
    public const string ConsentRequired = "AI_CONSENT_REQUIRED";
    public const string BudgetExceeded = "AI_BUDGET_EXCEEDED";
    public const string BudgetPolicyConflict = "AI_BUDGET_POLICY_CONFLICT";
    public const string BudgetConfirmationRequired = "AI_BUDGET_CONFIRMATION_REQUIRED";
    public const string ProviderUnavailable = "AI_PROVIDER_UNAVAILABLE";
    public const string RateLimited = "AI_RATE_LIMITED";
    public const string PayloadTooLarge = "AI_PAYLOAD_TOO_LARGE";
    public const string SchemaInvalid = "AI_SCHEMA_INVALID";
    public const string SourceStale = "AI_SOURCE_STALE";
    public const string DraftAlreadyConfirmed = "AI_DRAFT_ALREADY_CONFIRMED";
    public const string DraftConcurrencyConflict = "AI_DRAFT_CONCURRENCY_CONFLICT";
    public const string DraftConfirmationInProgress = "AI_DRAFT_CONFIRMATION_IN_PROGRESS";
    public const string JobNotCancelable = "AI_JOB_NOT_CANCELABLE";
    public const string JobNotRetryable = "AI_JOB_NOT_RETRYABLE";
    public const string IdempotencyConflict = "AI_IDEMPOTENCY_CONFLICT";
    public const string PlatformDisabled = "AI_JOB_PLATFORM_DISABLED";
    public const string WorkerPaused = "AI_WORKER_PAUSED";
    public const string InvalidRequest = "AI_INVALID_REQUEST";
    public const string SkillCatalogConflict = "AI_SKILL_CATALOG_CONFLICT";
    public const string SkillConcurrencyConflict = "AI_SKILL_CONCURRENCY_CONFLICT";
    public const string TaskSkillConcurrencyConflict = "AI_TASK_SKILL_CONCURRENCY_CONFLICT";
    public const string SkillOrganizationRequired = "AI_SKILL_ORGANIZATION_REQUIRED";
    public const string SkillSemanticInvalid = "AI_SKILL_SEMANTIC_INVALID";
}
