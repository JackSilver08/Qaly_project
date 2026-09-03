using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.Privacy;

namespace Qaly.UnitTests;

public sealed class PrivacyWorkProcessorTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<IVectorStorageService> _vector = new();
    private readonly TestPayloadProtector _protector = new();
    private readonly PrivacyV4Options _options = new()
    {
        Enabled = true,
        WorkerEnabled = true,
        ExportLifetimeMinutes = 30
    };

    public PrivacyWorkProcessorTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _vector
            .Setup(service => service.DeleteByFilterAsync(
                It.IsAny<VectorFilter>(),
                "qaly_context",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ProcessAsync_RetentionRedaction_RemovesSensitiveDerivedContentAndInvalidatesStores()
    {
        var fixture = await SeedMeetingRetentionAsync();
        var lease = Claim(fixture.Action);

        await CreateProcessor().ProcessAsync(lease, "privacy-test");

        fixture.Action.Status.Should().Be(PrivacyWorkerStatuses.Completed);
        fixture.Meeting.PrivacyState.Should().Be(MeetingPrivacyStates.Redacted);
        fixture.Meeting.TranscriptText.Should().BeEmpty();
        fixture.Meeting.RawPayloadJson.Should().Be("{}");
        fixture.Job.RequestJson.Should().Be("{}");
        fixture.Job.ResultJson.Should().BeNull();
        fixture.Draft.WorkingPayloadJson.Should().Be("{}");
        fixture.Draft.Status.Should().Be(AiDraftStatuses.Expired);
        (await _db.AiPromptCache.CountAsync()).Should().Be(0);
        _vector.Verify(service => service.DeleteByFilterAsync(
            It.Is<VectorFilter>(filter => filter.ProjectId == fixture.ProjectId && filter.OwnerId == fixture.UserId),
            "qaly_context",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ExpiredRetentionLease_DoesNotMutateCanonicalData()
    {
        var fixture = await SeedMeetingRetentionAsync();
        var lease = Claim(fixture.Action);
        fixture.Action.LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _db.SaveChangesAsync();

        await CreateProcessor().ProcessAsync(lease, "privacy-test");

        fixture.Action.Status.Should().Be(PrivacyWorkerStatuses.Running);
        fixture.Meeting.TranscriptText.Should().Be("sensitive transcript");
        _vector.Verify(service => service.DeleteByFilterAsync(
            It.IsAny<VectorFilter>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_LeaseLostDuringVectorInvalidation_DiscardsStaleDatabaseMutation()
    {
        var fixture = await SeedMeetingRetentionAsync();
        var lease = Claim(fixture.Action);
        _vector
            .Setup(service => service.DeleteByFilterAsync(
                It.IsAny<VectorFilter>(),
                "qaly_context",
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                fixture.Action.LeaseOwner = "privacy-new-owner";
                fixture.Action.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2);
                await _db.SaveChangesAsync();
            });

        await CreateProcessor().ProcessAsync(lease, "privacy-test");

        var persistedAction = await _db.PrivacyRetentionActions.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.Action.Id);
        var persistedMeeting = await _db.MeetingImports.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.Meeting.Id);
        persistedAction.Status.Should().Be(PrivacyWorkerStatuses.Running);
        persistedAction.LeaseOwner.Should().Be("privacy-new-owner");
        persistedMeeting.TranscriptText.Should().Be("sensitive transcript");
        persistedMeeting.PrivacyState.Should().Be(MeetingPrivacyStates.Active);
    }

    [Fact]
    public async Task ProcessAsync_ActiveLegalHold_PreservesMeetingAndMarksHeld()
    {
        var fixture = await SeedMeetingRetentionAsync();
        _db.PrivacyLegalHolds.Add(new PrivacyLegalHold
        {
            TenantId = fixture.TenantId,
            ProjectId = fixture.ProjectId,
            SubjectUserId = fixture.UserId,
            Status = PrivacyLegalHoldStatuses.Active,
            Reason = "Litigation",
            HeldById = Guid.NewGuid()
        });
        await _db.SaveChangesAsync();
        var lease = Claim(fixture.Action);

        await CreateProcessor().ProcessAsync(lease, "privacy-test");

        fixture.Action.Status.Should().Be(PrivacyWorkerStatuses.LegalHold);
        fixture.Meeting.PrivacyState.Should().Be(MeetingPrivacyStates.LegalHold);
        fixture.Meeting.TranscriptText.Should().Be("sensitive transcript");
        _vector.Verify(service => service.DeleteByFilterAsync(
            It.IsAny<VectorFilter>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_DsarExport_ProducesEncryptedTimeLimitedHumanAndMachineArtifact()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = await SeedDataSubjectRequestAsync(tenantId, userId, DataSubjectRequestTypes.Export);

        await CreateProcessor().ProcessAsync(
            new PrivacyWorkLease(PrivacyWorkKinds.DataSubjectRequest, request.Id, 1, DateTimeOffset.UtcNow.AddMinutes(2)),
            "privacy-test");

        request.Status.Should().Be(DataSubjectRequestStatuses.Completed);
        request.EncryptedResultPayload.Should().NotBeNullOrWhiteSpace();
        request.ResultExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        var plaintext = _protector.Unprotect(request.EncryptedResultPayload!);
        plaintext.Should().Contain("humanReadable");
        plaintext.Should().Contain("machineReadable");
        plaintext.Should().Contain("subject@qaly.dev");
        plaintext.Should().NotContain("PasswordHash");
        plaintext.Should().NotContain("password-hash");
    }

    [Fact]
    public async Task ProcessAsync_ExpiredDsarLease_DoesNotPublishExportArtifact()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = await SeedDataSubjectRequestAsync(tenantId, userId, DataSubjectRequestTypes.Export);
        request.LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _db.SaveChangesAsync();

        await CreateProcessor().ProcessAsync(
            new PrivacyWorkLease(PrivacyWorkKinds.DataSubjectRequest, request.Id, 1, DateTimeOffset.UtcNow.AddMinutes(2)),
            "privacy-test");

        request.Status.Should().Be(DataSubjectRequestStatuses.Collecting);
        request.EncryptedResultPayload.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_DsarDelete_AnonymizesSubjectAndRecordsBackupExpiryAsPartial()
    {
        var fixture = await SeedMeetingRetentionAsync();
        var request = new DataSubjectRequest
        {
            TenantId = fixture.TenantId,
            ProjectId = fixture.ProjectId,
            RequesterUserId = fixture.UserId,
            SubjectUserId = fixture.UserId,
            RequestType = DataSubjectRequestTypes.Delete,
            ScopeJson = "{\"scope\":\"all\"}",
            Status = DataSubjectRequestStatuses.Collecting,
            LeaseOwner = "privacy-test",
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
            AttemptCount = 1,
            MaxAttempts = 5
        };
        _db.DataSubjectRequests.Add(request);
        await _db.SaveChangesAsync();

        await CreateProcessor().ProcessAsync(
            new PrivacyWorkLease(PrivacyWorkKinds.DataSubjectRequest, request.Id, 1, request.LeaseExpiresAt!.Value),
            "privacy-test");

        request.Status.Should().Be(DataSubjectRequestStatuses.PartiallyCompleted);
        request.ResultSummaryJson.Should().Contain("backupExpiryPending");
        fixture.Meeting.PrivacyState.Should().Be(MeetingPrivacyStates.Deleted);
        fixture.Job.RequestJson.Should().Be("{}");
        var user = await _db.Users.SingleAsync(item => item.Id == fixture.UserId);
        user.IsActive.Should().BeFalse();
        user.Email.Should().EndWith("@invalid.local");
        _vector.Verify(service => service.DeleteByFilterAsync(
            It.Is<VectorFilter>(filter => filter.ProjectId == fixture.ProjectId && filter.OwnerId == fixture.UserId),
            "qaly_context",
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private PrivacyWorkProcessor CreateProcessor()
        => new(_db, _protector, _vector.Object, Options.Create(_options));

    private PrivacyWorkLease Claim(PrivacyRetentionAction action)
    {
        action.Status = PrivacyWorkerStatuses.Running;
        action.LeaseOwner = "privacy-test";
        action.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2);
        action.AttemptCount = 1;
        _db.SaveChanges();
        return new PrivacyWorkLease(
            PrivacyWorkKinds.Retention,
            action.Id,
            action.AttemptCount,
            action.LeaseExpiresAt.Value);
    }

    private async Task<DataSubjectRequest> SeedDataSubjectRequestAsync(
        Guid tenantId,
        Guid userId,
        string requestType)
    {
        _db.Users.Add(new User
        {
            Id = userId,
            FullName = "Data Subject",
            Email = "subject@qaly.dev",
            PasswordHash = "password-hash"
        });
        var request = new DataSubjectRequest
        {
            TenantId = tenantId,
            RequesterUserId = userId,
            SubjectUserId = userId,
            RequestType = requestType,
            ScopeJson = "{\"scope\":\"all\"}",
            Status = DataSubjectRequestStatuses.Collecting,
            LeaseOwner = "privacy-test",
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
            AttemptCount = 1,
            MaxAttempts = 5
        };
        _db.DataSubjectRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    private async Task<MeetingFixture> SeedMeetingRetentionAsync()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = userId,
            FullName = "Data Subject",
            Email = "subject@qaly.dev",
            PasswordHash = "password-hash"
        });
        _db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Privacy project",
            Code = "PRIV",
            OwnerId = userId,
            OrganizationId = tenantId
        });
        var policy = new RetentionPolicy
        {
            TenantId = tenantId,
            ProjectId = projectId,
            Name = "Meeting policy",
            Purpose = PrivacyPurposes.MeetingActionExtraction,
            PolicyVersion = "privacy-test-v1",
            CreatedById = userId,
            ExpiryAction = PrivacyExpiryActions.Redact
        };
        var job = new AiJob
        {
            TenantId = tenantId,
            ProjectId = projectId,
            RequestedById = userId,
            JobType = "meeting_action_extract",
            RequestJson = "{\"prompt\":\"sensitive\"}",
            RequestHash = Guid.NewGuid().ToString("N"),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CacheKey = Guid.NewGuid().ToString("N"),
            ResultJson = "{\"summary\":\"sensitive\"}",
            Status = AiJobStatuses.Succeeded
        };
        var draft = new AiGeneratedDraft
        {
            AiJobId = job.Id,
            ProjectId = projectId,
            PayloadJson = "{\"action\":\"sensitive\"}",
            OriginalPayloadJson = "{\"action\":\"sensitive\"}",
            WorkingPayloadJson = "{\"action\":\"sensitive\"}",
            Status = AiDraftStatuses.PendingReview
        };
        job.Drafts.Add(draft);
        var meeting = new MeetingImport
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ImportedById = userId,
            SourceProvider = "qaly-meet",
            SourceId = Guid.NewGuid().ToString(),
            SourceHash = new string('a', 64),
            Title = "Sensitive meeting",
            TranscriptText = "sensitive transcript",
            Summary = "sensitive summary",
            ParticipantsJson = "[\"Data Subject\"]",
            RawPayloadJson = "{\"sensitive\":true}",
            PrivacyState = MeetingPrivacyStates.Active,
            RetentionPolicyId = policy.Id,
            AiJobId = job.Id,
            AiDraftId = draft.Id
        };
        var action = new PrivacyRetentionAction
        {
            TenantId = tenantId,
            ProjectId = projectId,
            RetentionPolicyId = policy.Id,
            EntityType = nameof(MeetingImport),
            EntityId = meeting.Id,
            ActionType = PrivacyExpiryActions.Redact,
            Status = PrivacyWorkerStatuses.Pending,
            DueAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        _db.RetentionPolicies.Add(policy);
        _db.AiJobs.Add(job);
        _db.MeetingImports.Add(meeting);
        _db.PrivacyRetentionActions.Add(action);
        _db.AiPromptCache.Add(new AiPromptCache
        {
            TenantId = tenantId,
            ProjectId = projectId,
            CacheKey = job.CacheKey,
            JobType = job.JobType,
            SchemaId = "meeting.v1",
            RequestHash = job.RequestHash,
            ResponseJson = "{\"sensitive\":true}"
        });
        await _db.SaveChangesAsync();
        return new MeetingFixture(tenantId, projectId, userId, meeting, job, draft, action);
    }

    private sealed record MeetingFixture(
        Guid TenantId,
        Guid ProjectId,
        Guid UserId,
        MeetingImport Meeting,
        AiJob Job,
        AiGeneratedDraft Draft,
        PrivacyRetentionAction Action);

    private sealed class TestPayloadProtector : IPrivacyPayloadProtector
    {
        public string Protect(string plaintext) => Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string protectedPayload) => Encoding.UTF8.GetString(Convert.FromBase64String(protectedPayload));
    }
}
