using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.Infrastructure.Services.AI;

public sealed class AiComplianceService : IAiComplianceService
{
    private static readonly string[] SensitiveMetadataFragments =
    [
        "transcript",
        "prompt",
        "password",
        "secret",
        "token",
        "payload",
        "content",
        "cookie",
        "session"
    ];

    private readonly QalyDbContext _context;
    private readonly PrivacyV4Options _options;

    public AiComplianceService(QalyDbContext context, IOptions<PrivacyV4Options>? options = null)
    {
        _context = context;
        _options = options?.Value ?? new PrivacyV4Options();
    }

    public async Task<bool> CanProcessInCloudAsync(
        Guid? tenantId,
        Guid? projectId,
        Guid? userId,
        bool isSensitive,
        CancellationToken cancellationToken = default)
    {
        if (!isSensitive)
        {
            return true;
        }

        if (!tenantId.HasValue || tenantId == Guid.Empty ||
            !projectId.HasValue || projectId == Guid.Empty ||
            !userId.HasValue || userId == Guid.Empty)
        {
            return false;
        }

        if (_options.Enabled)
        {
            // Sensitive v4 calls must carry the exact policy and consent IDs so the decision is auditable.
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var cloudPolicy = await _context.AiBudgetPolicies
            .AsNoTracking()
            .Where(policy => policy.TenantId == tenantId &&
                (policy.ProjectId == projectId || policy.ProjectId == null))
            .OrderByDescending(policy => policy.ProjectId == projectId)
            .FirstOrDefaultAsync(cancellationToken);

        if (cloudPolicy?.AllowCloudForSensitive != true)
        {
            return false;
        }

        return await _context.PrivacyConsents
            .AsNoTracking()
            .AnyAsync(consent =>
                consent.TenantId == tenantId &&
                (consent.ProjectId == projectId || consent.ProjectId == null) &&
                consent.UserId == userId &&
                consent.ConsentType == PrivacyPurposes.AiCloudProcessing &&
                consent.Status == PrivacyConsentStatuses.Granted &&
                (consent.ExpiresAt == null || consent.ExpiresAt > now),
                cancellationToken);
    }

    public async Task<PrivacyProcessingDecision> EvaluateProcessingAsync(
        PrivacyProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var providerClass = NormalizeProviderClass(request.ProviderClass);

        if (!PrivacyDataClasses.IsSensitive(request.DataClassification))
        {
            return new PrivacyProcessingDecision(
                true,
                true,
                true,
                null,
                "The data classification does not require sensitive-data controls.",
                request.TenantId,
                request.ProjectId,
                request.RetentionPolicyId,
                null,
                request.ConsentId,
                request.RetentionDays,
                null,
                null,
                providerClass,
                now);
        }

        if (!_options.Enabled)
        {
            var legacyCloudEligible = await CanProcessInCloudAsync(
                request.TenantId,
                request.ProjectId,
                request.UserId,
                true,
                cancellationToken);
            var legacyLocalEligible = providerClass is PrivacyProviderClasses.Local or PrivacyProviderClasses.Any;
            return new PrivacyProcessingDecision(
                providerClass == PrivacyProviderClasses.Cloud ? legacyCloudEligible : legacyLocalEligible || legacyCloudEligible,
                legacyCloudEligible,
                legacyLocalEligible,
                legacyCloudEligible || legacyLocalEligible ? null : PrivacyErrorCodes.CloudBlocked,
                "Privacy v4 enforcement is disabled; the compatibility policy was evaluated.",
                request.TenantId,
                request.ProjectId,
                null,
                null,
                null,
                request.RetentionDays,
                null,
                null,
                providerClass,
                now);
        }

        if (request.TenantId == Guid.Empty || request.ProjectId == Guid.Empty || request.UserId == Guid.Empty)
        {
            return PrivacyProcessingDecision.Blocked(
                request,
                PrivacyErrorCodes.TenantRequired,
                "Tenant, project, and user identity are required for sensitive processing.",
                now);
        }

        if (!request.RetentionPolicyId.HasValue)
        {
            return PrivacyProcessingDecision.Blocked(
                request,
                PrivacyErrorCodes.PolicyRequired,
                "A configured retention policy is required for sensitive processing.",
                now);
        }

        var policy = await _context.RetentionPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.RetentionPolicyId.Value, cancellationToken);
        if (policy == null)
        {
            return PrivacyProcessingDecision.Blocked(
                request,
                PrivacyErrorCodes.PolicyRequired,
                "The selected retention policy was not found.",
                now,
                request.RetentionPolicyId);
        }

        if (!policy.IsActive || policy.EffectiveFrom > now ||
            (policy.EffectiveUntil.HasValue && policy.EffectiveUntil <= now))
        {
            return PrivacyProcessingDecision.Blocked(
                request,
                PrivacyErrorCodes.PolicyInactive,
                "The selected retention policy is not currently active.",
                now,
                policy.Id,
                policy.PolicyVersion);
        }

        if (policy.TenantId != request.TenantId ||
            (policy.ProjectId.HasValue && policy.ProjectId != request.ProjectId) ||
            !string.Equals(policy.DataClassification, request.DataClassification, StringComparison.Ordinal) ||
            !string.Equals(policy.Purpose, request.Purpose, StringComparison.Ordinal))
        {
            return PrivacyProcessingDecision.Blocked(
                request,
                PrivacyErrorCodes.PolicyMismatch,
                "The selected policy does not match the tenant, project, classification, or purpose.",
                now,
                policy.Id,
                policy.PolicyVersion);
        }

        var retentionDays = request.RetentionDays ?? policy.DefaultRetentionDays;
        var policyAllowedDays = ParseAllowedDays(policy.AllowedRetentionDaysJson);
        if (retentionDays <= 0 ||
            !policyAllowedDays.Contains(retentionDays) ||
            !_options.AllowedRetentionDays.Contains(retentionDays))
        {
            return PrivacyProcessingDecision.Blocked(
                request,
                PrivacyErrorCodes.RetentionUnsupported,
                "The requested retention duration is not permitted by the effective policy.",
                now,
                policy.Id,
                policy.PolicyVersion);
        }

        var consentRequired = policy.RequireExplicitConsent || providerClass is PrivacyProviderClasses.Cloud or PrivacyProviderClasses.Any;
        PrivacyConsent? consent = null;
        if (consentRequired)
        {
            if (!request.ConsentId.HasValue)
            {
                return PrivacyProcessingDecision.Blocked(
                    request,
                    PrivacyErrorCodes.ConsentRequired,
                    "Explicit purpose-bound consent is required.",
                    now,
                    policy.Id,
                    policy.PolicyVersion);
            }

            consent = await _context.PrivacyConsents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.ConsentId.Value, cancellationToken);
            var consentError = ValidateConsent(consent, request, policy, providerClass, now);
            if (consentError != null)
            {
                return PrivacyProcessingDecision.Blocked(
                    request,
                    consentError.Value.ErrorCode,
                    consentError.Value.Reason,
                    now,
                    policy.Id,
                    policy.PolicyVersion,
                    consent?.Id ?? request.ConsentId);
            }
        }

        var consentAllowsCloud = consent == null || consent.ProviderClass is PrivacyProviderClasses.Cloud or PrivacyProviderClasses.Any;
        var consentAllowsLocal = consent == null || consent.ProviderClass is PrivacyProviderClasses.Local or PrivacyProviderClasses.Any;
        var cloudPolicy = await _context.AiBudgetPolicies
            .AsNoTracking()
            .Where(item => item.TenantId == request.TenantId &&
                (item.ProjectId == request.ProjectId || item.ProjectId == null))
            .OrderByDescending(item => item.ProjectId == request.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        var cloudEligible = policy.AllowCloudProcessing &&
            cloudPolicy?.AllowCloudForSensitive == true &&
            consentAllowsCloud;
        var localEligible = policy.AllowLocalProcessing && consentAllowsLocal;
        var allowed = providerClass switch
        {
            PrivacyProviderClasses.Cloud => cloudEligible,
            PrivacyProviderClasses.Local => localEligible,
            PrivacyProviderClasses.Any => cloudEligible || localEligible,
            _ => false
        };

        if (!allowed)
        {
            var errorCode = providerClass == PrivacyProviderClasses.Local
                ? PrivacyErrorCodes.LocalBlocked
                : PrivacyErrorCodes.CloudBlocked;
            return PrivacyProcessingDecision.Blocked(
                request,
                errorCode,
                "No provider class is eligible under the effective policy and consent.",
                now,
                policy.Id,
                policy.PolicyVersion,
                consent?.Id);
        }

        return new PrivacyProcessingDecision(
            true,
            cloudEligible,
            localEligible,
            null,
            "Sensitive processing is permitted by the effective policy and consent.",
            request.TenantId,
            request.ProjectId,
            policy.Id,
            policy.PolicyVersion,
            consent?.Id,
            retentionDays,
            now.AddDays(retentionDays),
            policy.ExpiryAction,
            providerClass,
            now);
    }

    public async Task LogAuditEventAsync(
        Guid? tenantId,
        Guid? projectId,
        Guid? actorUserId,
        string eventType,
        string entityType,
        long? entityId,
        string? beforeJson,
        string? afterJson,
        CancellationToken cancellationToken = default)
        => await LogJobAuditEventAsync(
            tenantId,
            projectId,
            actorUserId,
            eventType,
            entityType,
            entityId,
            beforeJson,
            afterJson,
            cancellationToken: cancellationToken);

    public async Task LogJobAuditEventAsync(
        Guid? tenantId,
        Guid? projectId,
        Guid? actorUserId,
        string eventType,
        string entityType,
        long? entityId,
        string? beforeJson,
        string? afterJson,
        Guid? entityGuid = null,
        Guid? aiJobId = null,
        Guid? providerAttemptId = null,
        CancellationToken cancellationToken = default)
    {
        _context.AiAuditEvents.Add(new AiAuditEvent
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ActorUserId = actorUserId,
            EventType = Limit(eventType, 120) ?? "AI_EVENT",
            EntityType = Limit(entityType, 120),
            EntityId = entityId,
            EntityGuid = entityGuid,
            EntityKey = entityGuid?.ToString(),
            AiJobId = aiJobId,
            ProviderAttemptId = providerAttemptId,
            BeforeJson = Limit(beforeJson, 4000),
            AfterJson = Limit(afterJson, 4000)
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LogPrivacyAuditEventAsync(
        PrivacyAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        var safeMetadata = record.Metadata
            .Take(20)
            .ToDictionary(
                pair => Limit(pair.Key, 80) ?? "metadata",
                pair => IsSensitiveMetadataKey(pair.Key) ? "[redacted]" : Limit(pair.Value, 200));

        _context.AiAuditEvents.Add(new AiAuditEvent
        {
            TenantId = record.TenantId,
            ProjectId = record.ProjectId,
            ActorUserId = record.ActorUserId,
            EventType = Limit(record.EventType, 120) ?? "PRIVACY_EVENT",
            EntityType = Limit(record.EntityType, 120),
            EntityGuid = record.EntityId,
            EntityKey = record.EntityId?.ToString(),
            AiJobId = record.AiJobId,
            ProviderAttemptId = record.ProviderAttemptId,
            PrivacyConsentId = record.PrivacyConsentId,
            RetentionPolicyId = record.RetentionPolicyId,
            DataSubjectRequestId = record.DataSubjectRequestId,
            Purpose = Limit(record.Purpose, 80),
            PolicyVersion = Limit(record.PolicyVersion, 80),
            DataClassification = Limit(record.DataClassification, 50),
            ProviderClass = Limit(record.ProviderClass, 30),
            Outcome = Limit(record.Outcome, 40),
            FailureCode = Limit(record.FailureCode, 100),
            RequestId = Limit(record.RequestId, 120),
            AfterJson = safeMetadata.Count == 0 ? null : JsonSerializer.Serialize(safeMetadata)
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static (string ErrorCode, string Reason)? ValidateConsent(
        PrivacyConsent? consent,
        PrivacyProcessingRequest request,
        RetentionPolicy policy,
        string providerClass,
        DateTimeOffset now)
    {
        if (consent == null)
        {
            return (PrivacyErrorCodes.ConsentInvalid, "The selected consent record was not found.");
        }

        if (consent.Status == PrivacyConsentStatuses.Revoked)
        {
            return (PrivacyErrorCodes.ConsentRevoked, "Consent has been revoked.");
        }

        if (consent.Status == PrivacyConsentStatuses.Expired ||
            (consent.ExpiresAt.HasValue && consent.ExpiresAt <= now))
        {
            return (PrivacyErrorCodes.ConsentExpired, "Consent has expired.");
        }

        if (consent.Status != PrivacyConsentStatuses.Granted ||
            consent.TenantId != request.TenantId ||
            (consent.ProjectId.HasValue && consent.ProjectId != request.ProjectId) ||
            consent.UserId != request.UserId ||
            consent.RetentionPolicyId != policy.Id ||
            !string.Equals(consent.PolicyVersion, policy.PolicyVersion, StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(request.NoticeVersion) &&
                !string.Equals(consent.NoticeVersion, request.NoticeVersion, StringComparison.Ordinal)) ||
            !string.Equals(consent.Purpose, request.Purpose, StringComparison.Ordinal) ||
            !string.Equals(consent.SourceType, request.SourceType, StringComparison.Ordinal) ||
            (consent.SourceEntityId.HasValue && consent.SourceEntityId != request.SourceEntityId))
        {
            return (PrivacyErrorCodes.ConsentInvalid, "Consent does not match the subject, source, purpose, or policy version.");
        }

        var providerAllowed = providerClass switch
        {
            PrivacyProviderClasses.Cloud => consent.ProviderClass is PrivacyProviderClasses.Cloud or PrivacyProviderClasses.Any,
            PrivacyProviderClasses.Local => consent.ProviderClass is PrivacyProviderClasses.Local or PrivacyProviderClasses.Any,
            PrivacyProviderClasses.Any => consent.ProviderClass == PrivacyProviderClasses.Any,
            _ => false
        };
        return providerAllowed
            ? null
            : (PrivacyErrorCodes.ConsentInvalid, "Consent does not permit the requested provider class.");
    }

    private static HashSet<int> ParseAllowedDays(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(value)?.Where(day => day > 0).ToHashSet() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeProviderClass(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            PrivacyProviderClasses.Cloud => PrivacyProviderClasses.Cloud,
            PrivacyProviderClasses.Local => PrivacyProviderClasses.Local,
            PrivacyProviderClasses.Any => PrivacyProviderClasses.Any,
            _ => PrivacyProviderClasses.Unknown
        };

    private static bool IsSensitiveMetadataKey(string key)
        => SensitiveMetadataFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string? Limit(string? value, int maximumLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maximumLength)];
}
