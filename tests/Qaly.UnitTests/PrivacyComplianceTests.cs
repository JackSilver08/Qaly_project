using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qaly.Application.Common.Models;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class PrivacyComplianceTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PrivacyComplianceTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    [Fact]
    public async Task EvaluateProcessingAsync_LocalPolicyAndPurposeBoundConsent_AllowsLocalOnly()
    {
        var (policy, consent) = await SeedPolicyAndConsentAsync(
            providerClass: PrivacyProviderClasses.Local,
            allowCloud: false);
        var service = CreateService();

        var decision = await service.EvaluateProcessingAsync(Request(policy.Id, consent.Id, PrivacyProviderClasses.Local));

        decision.Allowed.Should().BeTrue();
        decision.LocalEligible.Should().BeTrue();
        decision.CloudEligible.Should().BeFalse();
        decision.RetentionDays.Should().Be(30);
        decision.RetentionExpiresAt.Should().NotBeNull();
        decision.PolicyVersion.Should().Be(policy.PolicyVersion);
    }

    [Fact]
    public async Task EvaluateProcessingAsync_CloudRequiresPolicyConsentAndCloudGateIntersection()
    {
        var (policy, consent) = await SeedPolicyAndConsentAsync(
            providerClass: PrivacyProviderClasses.Any,
            allowCloud: true);
        var service = CreateService();

        var blocked = await service.EvaluateProcessingAsync(Request(policy.Id, consent.Id, PrivacyProviderClasses.Any));
        blocked.Allowed.Should().BeTrue("the local path remains eligible");
        blocked.CloudEligible.Should().BeFalse("the tenant cloud gate is missing");

        _db.AiBudgetPolicies.Add(new AiBudgetPolicy
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            AllowCloudForSensitive = true
        });
        await _db.SaveChangesAsync();

        var allowed = await service.EvaluateProcessingAsync(Request(policy.Id, consent.Id, PrivacyProviderClasses.Any));
        allowed.Allowed.Should().BeTrue();
        allowed.CloudEligible.Should().BeTrue();
        allowed.LocalEligible.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateProcessingAsync_RevokedConsent_ReturnsStructuredBlock()
    {
        var (policy, consent) = await SeedPolicyAndConsentAsync(
            providerClass: PrivacyProviderClasses.Local,
            allowCloud: false);
        consent.Status = PrivacyConsentStatuses.Revoked;
        consent.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var decision = await CreateService().EvaluateProcessingAsync(
            Request(policy.Id, consent.Id, PrivacyProviderClasses.Local));

        decision.Allowed.Should().BeFalse();
        decision.ErrorCode.Should().Be(PrivacyErrorCodes.ConsentRevoked);
    }

    [Fact]
    public async Task EvaluateProcessingAsync_NoticeOrRetentionMismatch_IsRejected()
    {
        var (policy, consent) = await SeedPolicyAndConsentAsync(
            providerClass: PrivacyProviderClasses.Local,
            allowCloud: false);
        var request = Request(policy.Id, consent.Id, PrivacyProviderClasses.Local) with
        {
            NoticeVersion = "different-notice",
            RetentionDays = 365
        };

        var decision = await CreateService().EvaluateProcessingAsync(request);

        decision.Allowed.Should().BeFalse();
        decision.ErrorCode.Should().Be(PrivacyErrorCodes.RetentionUnsupported);
    }

    [Fact]
    public async Task LogPrivacyAuditEventAsync_RedactsSensitiveMetadata()
    {
        await CreateService().LogPrivacyAuditEventAsync(new PrivacyAuditRecord
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            ActorUserId = _userId,
            EventType = "PRIVACY_TEST",
            EntityType = "MeetingImport",
            EntityId = Guid.NewGuid(),
            Outcome = "blocked",
            Metadata = new Dictionary<string, string?>
            {
                ["transcript"] = "secret meeting text",
                ["sourceId"] = "meeting-42"
            }
        });

        var audit = await _db.AiAuditEvents.SingleAsync();
        audit.AfterJson.Should().NotContain("secret meeting text");
        audit.AfterJson.Should().Contain("[redacted]");
        audit.AfterJson.Should().Contain("meeting-42");
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private AiComplianceService CreateService()
        => new(_db, Options.Create(new PrivacyV4Options
        {
            Enabled = true,
            EnforceSensitiveIngestion = true,
            AllowedRetentionDays = [7, 30, 90]
        }));

    private PrivacyProcessingRequest Request(Guid policyId, Guid consentId, string providerClass)
        => new()
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            UserId = _userId,
            Purpose = PrivacyPurposes.MeetingActionExtraction,
            DataClassification = PrivacyDataClasses.SensitiveCollaboration,
            ProviderClass = providerClass,
            ConsentId = consentId,
            RetentionPolicyId = policyId,
            NoticeVersion = "privacy-v4",
            RetentionDays = 30,
            SourceType = "meeting",
            SourceEntityId = null
        };

    private async Task<(RetentionPolicy Policy, PrivacyConsent Consent)> SeedPolicyAndConsentAsync(
        string providerClass,
        bool allowCloud)
    {
        var policy = new RetentionPolicy
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            Name = "Meeting policy",
            DataClassification = PrivacyDataClasses.SensitiveCollaboration,
            Purpose = PrivacyPurposes.MeetingActionExtraction,
            AllowedRetentionDaysJson = "[7,30,90]",
            DefaultRetentionDays = 30,
            ExpiryAction = PrivacyExpiryActions.Redact,
            AllowCloudProcessing = allowCloud,
            AllowLocalProcessing = true,
            RequireExplicitConsent = true,
            IsActive = true,
            PolicyVersion = $"test-{Guid.NewGuid():N}",
            CreatedById = _userId,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var consent = new PrivacyConsent
        {
            TenantId = _tenantId,
            ProjectId = _projectId,
            UserId = _userId,
            ConsentType = PrivacyPurposes.AiCloudProcessing,
            Purpose = PrivacyPurposes.MeetingActionExtraction,
            SourceType = "meeting",
            ProviderClass = providerClass,
            PolicyVersion = policy.PolicyVersion,
            NoticeVersion = "privacy-v4",
            RetentionPolicyId = policy.Id,
            Status = PrivacyConsentStatuses.Granted,
            GrantedAt = DateTimeOffset.UtcNow
        };
        _db.RetentionPolicies.Add(policy);
        _db.PrivacyConsents.Add(consent);
        await _db.SaveChangesAsync();
        return (policy, consent);
    }
}
