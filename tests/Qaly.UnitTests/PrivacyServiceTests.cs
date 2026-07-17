using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Privacy;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.Privacy;

namespace Qaly.UnitTests;

public sealed class PrivacyServiceTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IAiComplianceService> _compliance = new();
    private readonly TestPayloadProtector _protector = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public PrivacyServiceTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _currentUser.SetupGet(service => service.UserId).Returns(_userId);
        _currentUser.SetupGet(service => service.Role).Returns(ProjectRoleRules.SystemAdmin);
        _currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);

        var user = new User
        {
            Id = _userId,
            FullName = "Privacy Admin",
            Email = "privacy-admin@qaly.test",
            PasswordHash = "test"
        };
        _db.Users.Add(user);
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Privacy Project",
            Code = "PRIV",
            OwnerId = _userId,
            Owner = user
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task PolicyConsentDecisionAndHealth_LifecyclePersistsAuditAndRetentionWork()
    {
        var service = CreateService();

        var created = await service.CreatePolicyAsync(PolicyRequest("Initial policy"));
        created.IsSuccess.Should().BeTrue();
        created.StatusCode.Should().Be(201);

        var listed = await service.ListPoliciesAsync(_projectId, _projectId);
        listed.Data.Should().ContainSingle(item => item.Id == created.Data!.Id);

        var superseded = await service.SupersedePolicyAsync(
            created.Data!.Id,
            PolicyRequest("Replacement policy"),
            created.Data.RowVersion);
        superseded.IsSuccess.Should().BeTrue();
        (await _db.RetentionPolicies.FindAsync(created.Data.Id))!.IsActive.Should().BeFalse();

        var disabled = await service.DisablePolicyAsync(
            superseded.Data!.Id,
            new RetentionPolicyDisableRequest("Policy retired", superseded.Data.RowVersion));
        disabled.Data!.IsActive.Should().BeFalse();

        var active = await service.CreatePolicyAsync(PolicyRequest("Consent policy"));
        var activePolicy = active.Data!;
        var granted = await service.GrantConsentAsync(new PrivacyConsentGrantRequest(
            _projectId,
            activePolicy.Id,
            PrivacyPurposes.MeetingActionExtraction,
            PrivacyProviderClasses.Local,
            "meeting",
            null,
            "privacy-v4",
            DateTimeOffset.UtcNow.AddDays(30)));
        granted.IsSuccess.Should().BeTrue();
        var grantedConsent = granted.Data!;

        var consents = await service.ListConsentsAsync(_projectId);
        consents.Data.Should().ContainSingle(item => item.Id == grantedConsent.Id);

        _compliance.Setup(service => service.EvaluateProcessingAsync(
                It.IsAny<PrivacyProcessingRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacyProcessingRequest request, CancellationToken _) => new PrivacyProcessingDecision(
                true,
                false,
                true,
                null,
                "allowed",
                request.TenantId,
                request.ProjectId,
                request.RetentionPolicyId,
                activePolicy.PolicyVersion,
                request.ConsentId,
                30,
                DateTimeOffset.UtcNow.AddDays(30),
                PrivacyExpiryActions.Redact,
                PrivacyProviderClasses.Local,
                DateTimeOffset.UtcNow));
        var decision = await service.EvaluateAsync(new PrivacyDecisionRequest(
            _projectId,
            activePolicy.Id,
            grantedConsent.Id,
            PrivacyPurposes.MeetingActionExtraction,
            PrivacyDataClasses.SensitiveCollaboration,
            PrivacyProviderClasses.Local,
            30,
            "meeting",
            null));
        decision.Data!.Allowed.Should().BeTrue();
        decision.Data.LocalEligible.Should().BeTrue();

        _db.MeetingImports.Add(new MeetingImport
        {
            TenantId = _projectId,
            ProjectId = _projectId,
            ImportedById = _userId,
            SourceId = "privacy-service-test",
            SourceHash = "privacy-service-test",
            Title = "Sensitive meeting",
            ConsentId = grantedConsent.Id,
            RetentionPolicyId = activePolicy.Id,
            PrivacyState = MeetingPrivacyStates.Active
        });
        await _db.SaveChangesAsync();

        var revoked = await service.RevokeConsentAsync(
            grantedConsent.Id,
            new PrivacyConsentRevokeRequest("User withdrew consent", grantedConsent.RowVersion));
        revoked.Data!.Status.Should().Be(PrivacyConsentStatuses.Revoked);
        (await _db.PrivacyRetentionActions.CountAsync()).Should().Be(1);

        var health = await service.GetHealthAsync();
        health.Data!.Status.Should().Be("degraded_worker_disabled");
        health.Data.PendingRetentionActions.Should().Be(1);
        (await _db.AiAuditEvents.CountAsync()).Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task DataSubjectRequests_ExportAndRejectLifecycleEnforcesIdempotencyAndAudit()
    {
        var service = CreateService();
        var submit = new DataSubjectRequestSubmitRequest(
            _projectId,
            _projectId,
            _userId,
            DataSubjectRequestTypes.Export,
            "project",
            "export-request-001");

        var created = await service.SubmitDataSubjectRequestAsync(submit);
        created.IsSuccess.Should().BeTrue();
        created.StatusCode.Should().Be(202);

        var replay = await service.SubmitDataSubjectRequestAsync(submit);
        replay.Data!.Id.Should().Be(created.Data!.Id);

        var conflict = await service.SubmitDataSubjectRequestAsync(submit with
        {
            RequestType = DataSubjectRequestTypes.Delete
        });
        conflict.IsSuccess.Should().BeFalse();
        conflict.ErrorCode.Should().Be(PrivacyErrorCodes.IdempotencyConflict);

        var listed = await service.ListDataSubjectRequestsAsync(_projectId);
        listed.Data.Should().ContainSingle(item => item.Id == created.Data.Id);
        (await service.GetDataSubjectRequestAsync(created.Data.Id)).Data!.Id.Should().Be(created.Data.Id);

        var accepted = await service.AcceptDataSubjectRequestAsync(
            created.Data.Id,
            new DataSubjectRequestDecisionRequest("Identity verified"));
        accepted.Data!.Status.Should().Be(DataSubjectRequestStatuses.Accepted);

        var entity = await _db.DataSubjectRequests.FindAsync(created.Data.Id);
        entity!.Status = DataSubjectRequestStatuses.Completed;
        entity.EncryptedResultPayload = _protector.Protect("{\"kind\":\"privacy-export\"}");
        entity.ResultContentType = "application/json";
        entity.ResultFileName = "privacy-export.json";
        entity.ResultExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync();

        var downloaded = await service.DownloadDataSubjectExportAsync(created.Data.Id);
        downloaded.IsSuccess.Should().BeTrue();
        Encoding.UTF8.GetString(downloaded.Data!.Content).Should().Contain("privacy-export");
        entity.DownloadCount.Should().Be(1);

        var delete = await service.SubmitDataSubjectRequestAsync(new DataSubjectRequestSubmitRequest(
            _projectId,
            _projectId,
            _userId,
            DataSubjectRequestTypes.Delete,
            "all",
            "delete-request-001"));
        var rejected = await service.RejectDataSubjectRequestAsync(
            delete.Data!.Id,
            new DataSubjectRequestDecisionRequest("Request withdrawn"));
        rejected.Data!.Status.Should().Be(DataSubjectRequestStatuses.Rejected);
        rejected.Data.RejectionReason.Should().Be("Request withdrawn");
    }

    [Fact]
    public async Task LegalHold_CreateReplayListAndReleaseMaintainsSingleActiveHold()
    {
        var service = CreateService();
        var request = new PrivacyLegalHoldCreateRequest(
            _projectId,
            _projectId,
            _userId,
            null,
            null,
            "Preserve records for an active review");

        var created = await service.CreateLegalHoldAsync(request);
        created.IsSuccess.Should().BeTrue();
        created.StatusCode.Should().Be(201);

        var replay = await service.CreateLegalHoldAsync(request);
        replay.Data!.Id.Should().Be(created.Data!.Id);
        (await _db.PrivacyLegalHolds.CountAsync()).Should().Be(1);

        var listed = await service.ListLegalHoldsAsync(_projectId);
        listed.Data.Should().ContainSingle(item => item.Status == PrivacyLegalHoldStatuses.Active);

        var released = await service.ReleaseLegalHoldAsync(
            created.Data.Id,
            new PrivacyLegalHoldReleaseRequest("Review completed", created.Data.RowVersion));
        released.Data!.Status.Should().Be(PrivacyLegalHoldStatuses.Released);
        released.Data.ReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task InvalidOrUnauthenticatedRequests_ReturnStructuredFailures()
    {
        var service = CreateService();
        var invalidPolicy = await service.CreatePolicyAsync(PolicyRequest("Invalid") with
        {
            AllowedRetentionDays = [999],
            DefaultRetentionDays = 999
        });
        invalidPolicy.ErrorCode.Should().Be(PrivacyErrorCodes.PolicyMismatch);

        var invalidDsar = await service.SubmitDataSubjectRequestAsync(new DataSubjectRequestSubmitRequest(
            _projectId,
            null,
            null,
            "unknown",
            "all",
            "short"));
        invalidDsar.ErrorCode.Should().Be(PrivacyErrorCodes.IdentityRequired);

        _currentUser.SetupGet(current => current.UserId).Returns((Guid?)null);
        (await service.ListConsentsAsync(null)).StatusCode.Should().Be(403);
        (await service.GetHealthAsync()).StatusCode.Should().Be(403);
        (await service.GetDataSubjectRequestAsync(Guid.NewGuid())).StatusCode.Should().Be(403);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private PrivacyService CreateService()
        => new(
            _db,
            _currentUser.Object,
            _compliance.Object,
            _protector,
            Options.Create(new PrivacyV4Options
            {
                Enabled = true,
                WorkerEnabled = false,
                EnforceSensitiveIngestion = true,
                AllowedRetentionDays = [7, 30, 90],
                MaxAttempts = 3,
                DsarDeadlineDays = 30
            }));

    private RetentionPolicyUpsertRequest PolicyRequest(string name)
        => new(
            _projectId,
            _projectId,
            name,
            PrivacyDataClasses.SensitiveCollaboration,
            PrivacyPurposes.MeetingActionExtraction,
            [7, 30, 90],
            30,
            PrivacyExpiryActions.Redact,
            false,
            true,
            true,
            _userId,
            null,
            null);

    private sealed class TestPayloadProtector : IPrivacyPayloadProtector
    {
        public string Protect(string plaintext) => Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string protectedPayload)
            => Encoding.UTF8.GetString(Convert.FromBase64String(protectedPayload));
    }
}
