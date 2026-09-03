using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class MeetingPrivacyEnforcementTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly PrivacyV4Options _options = new()
    {
        Enabled = true,
        EnforceSensitiveIngestion = true,
        MaxPayloadCharacters = 80_000,
        AllowedRetentionDays = [7, 30, 90]
    };
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IAiGateway> _gateway = new();
    private readonly Mock<ITaskService> _taskService = new();
    private readonly Mock<IAuditLogService> _audit = new();

    public MeetingPrivacyEnforcementTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _currentUser.SetupGet(service => service.UserId).Returns(_userId);
        _currentUser.SetupGet(service => service.Role).Returns(ProjectRoleRules.Member);
        _db.Users.Add(new User { Id = _userId, FullName = "Member", Email = "member@qaly.dev" });
        _db.Projects.Add(new Project { Id = _projectId, Name = "Qaly", Code = "QALY", OwnerId = _userId });
        _db.ProjectMembers.Add(new ProjectMember { ProjectId = _projectId, UserId = _userId, Role = ProjectRoleRules.Owner });
        _db.SaveChanges();
    }

    [Fact]
    public async Task ImportMeetilyAsync_WithEffectivePolicy_StoresSensitiveEvidenceAndSchedulesRetention()
    {
        var (policy, consent) = await SeedPolicyAndConsentAsync();
        var result = await CreateService().ImportMeetilyAsync(Request(policy.Id, consent.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var meeting = await _db.MeetingImports.SingleAsync();
        meeting.TenantId.Should().Be(_projectId);
        meeting.DataClassification.Should().Be(PrivacyDataClasses.SensitiveCollaboration);
        meeting.PrivacyState.Should().Be(MeetingPrivacyStates.Active);
        meeting.ConsentId.Should().Be(consent.Id);
        meeting.RetentionPolicyId.Should().Be(policy.Id);
        meeting.RetentionExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(29));
        var action = await _db.PrivacyRetentionActions.SingleAsync();
        action.EntityId.Should().Be(meeting.Id);
        action.ActionType.Should().Be(PrivacyExpiryActions.Redact);
        (await _db.AiAuditEvents.AnyAsync(audit => audit.EventType == "MEETING_SENSITIVE_INGESTED")).Should().BeTrue();
    }

    [Fact]
    public async Task ImportMeetilyAsync_WithoutConsent_BlocksBeforePersistingContent()
    {
        var (policy, _) = await SeedPolicyAndConsentAsync();
        var result = await CreateService().ImportMeetilyAsync(Request(policy.Id, consentId: null));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(PrivacyErrorCodes.ConsentRequired);
        (await _db.MeetingImports.CountAsync()).Should().Be(0);
        (await _db.AiJobs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportMeetilyAsync_OversizedPayload_BlocksBeforePolicyEvaluation()
    {
        _options.MaxPayloadCharacters = 100;
        var result = await CreateService().ImportMeetilyAsync(new MeetilyImportRequest(
            _projectId,
            "Meeting",
            "source",
            DateTimeOffset.UtcNow,
            null,
            new string('x', 101),
            [],
            [],
            null));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(413);
        result.ErrorCode.Should().Be(PrivacyErrorCodes.PayloadTooLarge);
        (await _db.MeetingImports.CountAsync()).Should().Be(0);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private MeetingImportService CreateService()
    {
        var compliance = new AiComplianceService(_db, Options.Create(_options));
        return new MeetingImportService(
            new GenericRepository<Project>(_db),
            new GenericRepository<ProjectMember>(_db),
            new GenericRepository<OrganizationMember>(_db),
            new GenericRepository<MeetingImport>(_db),
            new GenericRepository<AiJob>(_db),
            new GenericRepository<AiGeneratedDraft>(_db),
            new GenericRepository<MeetingActionItemMapping>(_db),
            new GenericRepository<TaskItem>(_db),
            new GenericRepository<GroupMeetingSession>(_db),
            _gateway.Object,
            _taskService.Object,
            new UnitOfWork(_db),
            _currentUser.Object,
            _audit.Object,
            new TaskAccessPolicy(
                _currentUser.Object,
                new GenericRepository<Project>(_db),
                new GenericRepository<ProjectMember>(_db),
                new GenericRepository<OrganizationMember>(_db),
                new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_db))),
            compliance,
            new GenericRepository<PrivacyRetentionAction>(_db),
            new GenericRepository<AiAuditEvent>(_db),
            Options.Create(_options));
    }

    private MeetilyImportRequest Request(Guid policyId, Guid? consentId)
        => new(
            _projectId,
            "Sprint planning",
            "meetily-privacy-1",
            DateTimeOffset.UtcNow,
            "Planning summary",
            "Action: prepare release evidence.",
            ["Member"],
            [new MeetilyActionItemInput("Prepare release evidence", null, null, "High", "Action line")],
            null,
            consentId,
            policyId,
            PrivacyProcessingModes.LocalOnly,
            30,
            "privacy-v4");

    private async Task<(RetentionPolicy Policy, PrivacyConsent Consent)> SeedPolicyAndConsentAsync()
    {
        var policy = new RetentionPolicy
        {
            TenantId = _projectId,
            ProjectId = _projectId,
            Name = "Meeting policy",
            DataClassification = PrivacyDataClasses.SensitiveCollaboration,
            Purpose = PrivacyPurposes.MeetingActionExtraction,
            AllowedRetentionDaysJson = "[7,30,90]",
            DefaultRetentionDays = 30,
            ExpiryAction = PrivacyExpiryActions.Redact,
            AllowLocalProcessing = true,
            RequireExplicitConsent = true,
            IsActive = true,
            PolicyVersion = $"test-{Guid.NewGuid():N}",
            CreatedById = _userId,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var consent = new PrivacyConsent
        {
            TenantId = _projectId,
            ProjectId = _projectId,
            UserId = _userId,
            ConsentType = PrivacyPurposes.MeetingActionExtraction,
            Purpose = PrivacyPurposes.MeetingActionExtraction,
            SourceType = "meeting",
            ProviderClass = PrivacyProviderClasses.Local,
            PolicyVersion = policy.PolicyVersion,
            NoticeVersion = "privacy-v4",
            RetentionPolicyId = policy.Id,
            Status = PrivacyConsentStatuses.Granted
        };
        _db.RetentionPolicies.Add(policy);
        _db.PrivacyConsents.Add(consent);
        await _db.SaveChangesAsync();
        return (policy, consent);
    }
}
