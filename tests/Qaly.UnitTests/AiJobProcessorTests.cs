using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiJobProcessorTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<IAiGateway> _gateway = new();
    private readonly Mock<IAiSourceGuard> _sourceGuard = new();
    private readonly Mock<IAiComplianceService> _compliance = new();
    private readonly Mock<IOptionsMonitor<AiJobPlatformOptions>> _options = new();
    private readonly AiJobPlatformOptions _platformOptions = new()
    {
        Enabled = true,
        WorkerEnabled = true,
        BaseRetrySeconds = 1
    };

    public AiJobProcessorTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _sourceGuard
            .Setup(guard => guard.ValidateAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Qaly.Application.DTOs.Ai.AiJobSourceInputDto>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSourceGuardResult(true));
        _options.SetupGet(options => options.CurrentValue).Returns(_platformOptions);
    }

    [Fact]
    public async Task ProcessAsync_ValidResult_PersistsOneResultDraftAndTerminalDispatch()
    {
        var seeded = await SeedRunningJobAsync("task_draft");
        const string resultJson = """
            {
              "title":"Ship worker",
              "description":"Durable AI job",
              "priority":"High",
              "deadline":null,
              "assignee_suggestion":null,
              "source_refs":[],
              "confidence":0.91
            }
            """;
        _gateway.Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = true,
                Content = resultJson,
                ProviderName = "Ollama",
                ModelName = "test-model",
                InputTokens = 10,
                OutputTokens = 20,
                EstimatedCostUsd = 0.001m
            });

        var processor = CreateProcessor();
        await processor.ProcessAsync(seeded.Lease, seeded.WorkerId);
        await processor.ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.Include(item => item.Dispatch).SingleAsync(item => item.Id == seeded.JobId);
        var attempt = await _db.AiProviderAttempts.SingleAsync(item => item.Id == seeded.AttemptId);
        job.Status.Should().Be(AiJobStatuses.Succeeded);
        job.ResultJson.Should().NotBeNullOrWhiteSpace();
        job.ProgressPercent.Should().Be(100);
        job.Dispatch!.CompletedAt.Should().NotBeNull();
        job.Dispatch.LeaseOwner.Should().BeNull();
        attempt.Status.Should().Be(AiAttemptStatuses.Succeeded);
        (await _db.AiGeneratedDrafts.CountAsync(draft => draft.AiJobId == job.Id)).Should().Be(1);
        var draft = await _db.AiGeneratedDrafts.SingleAsync(item => item.AiJobId == job.Id);
        draft.Status.Should().Be(AiDraftStatuses.PendingReview);
        draft.OriginalPayloadJson.Should().Be(draft.WorkingPayloadJson);
        _gateway.Verify(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_RetryableFailure_SchedulesBackoffAndReleasesLease()
    {
        var seeded = await SeedRunningJobAsync("progress_summary");
        _gateway.Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = false,
                ErrorCode = AiErrorCodes.ProviderUnavailable,
                ErrorMessage = "provider down",
                Retryable = true
            });

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.Include(item => item.Dispatch).SingleAsync(item => item.Id == seeded.JobId);
        var attempt = await _db.AiProviderAttempts.SingleAsync(item => item.Id == seeded.AttemptId);
        job.Status.Should().Be(AiJobStatuses.Retrying);
        job.NextRetryAt.Should().NotBeNull();
        job.LastErrorCode.Should().Be(AiErrorCodes.ProviderUnavailable);
        job.Dispatch!.LeaseOwner.Should().BeNull();
        job.Dispatch.CompletedAt.Should().BeNull();
        attempt.Status.Should().Be(AiAttemptStatuses.Failed);
        attempt.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_CanceledJob_DiscardsProviderWorkAndCompletesAttempt()
    {
        var seeded = await SeedRunningJobAsync("task_draft", AiJobStatuses.Canceled);

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.Include(item => item.Dispatch).SingleAsync(item => item.Id == seeded.JobId);
        var attempt = await _db.AiProviderAttempts.SingleAsync(item => item.Id == seeded.AttemptId);
        job.Status.Should().Be(AiJobStatuses.Canceled);
        job.Dispatch!.CompletedAt.Should().NotBeNull();
        attempt.Status.Should().Be(AiAttemptStatuses.Canceled);
        _gateway.Verify(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WorkerDisabledDuringProviderCall_ReleasesLeaseWithoutCancelingJob()
    {
        var seeded = await SeedRunningJobAsync("progress_summary");
        _gateway.Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _platformOptions.WorkerEnabled = false;
                return new AiResponse
                {
                    IsSuccess = true,
                    Content = "{\"summary\":\"discarded while paused\"}",
                    ProviderName = "Ollama",
                    ModelName = "test-model"
                };
            });

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.Include(item => item.Dispatch).SingleAsync(item => item.Id == seeded.JobId);
        var attempt = await _db.AiProviderAttempts.SingleAsync(item => item.Id == seeded.AttemptId);
        job.Status.Should().Be(AiJobStatuses.Retrying);
        job.CanceledAt.Should().BeNull();
        job.ResultJson.Should().BeNull();
        job.LastErrorCode.Should().Be(AiErrorCodes.WorkerPaused);
        job.Dispatch!.CompletedAt.Should().BeNull();
        job.Dispatch.LeaseOwner.Should().BeNull();
        attempt.Status.Should().Be(AiAttemptStatuses.Failed);
        attempt.ErrorCode.Should().Be(AiErrorCodes.WorkerPaused);
        (await _db.AiGeneratedDrafts.CountAsync(draft => draft.AiJobId == job.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_SelectedMessages_GroundsPromptInExactAuthorizedContent()
    {
        var user = new User { FullName = "Release Owner", Email = "release-owner@qaly.test", PasswordHash = "test" };
        var group = new WorkGroup { Name = "Release", OwnerId = user.Id };
        var project = new Project { Name = "Release Project", Code = "REL", OwnerId = user.Id, SourceGroupId = group.Id };
        var selected = new GroupMessage
        {
            WorkGroupId = group.Id,
            UserId = user.Id,
            User = user,
            Content = "Selected: update the deployment checklist"
        };
        var unselected = new GroupMessage
        {
            WorkGroupId = group.Id,
            UserId = user.Id,
            User = user,
            Content = "Unselected: cancel the release"
        };
        _db.AddRange(user, group, project, selected, unselected);
        await _db.SaveChangesAsync();
        var seeded = await SeedRunningJobAsync(
            "chat_summary",
            source: new AiJobSource
            {
                SourceType = "message",
                SourceEntityId = selected.Id,
                SourceHash = new string('b', 64),
                SortOrder = 0
            },
            projectId: project.Id,
            userId: user.Id);
        AiRequest? captured = null;
        _gateway.Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = true,
                Content = "{\"summary\":\"grounded\"}",
                ProviderName = "test",
                ModelName = "test"
            });

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        captured.Should().NotBeNull();
        captured!.Prompt.Should().Contain(selected.Content);
        captured.Prompt.Should().Contain($"/groups/{group.Id}?messageId={selected.Id}");
        captured.Prompt.Should().NotContain(unselected.Content);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private AiJobProcessor CreateProcessor()
        => new(
            _db,
            _gateway.Object,
            _sourceGuard.Object,
            _compliance.Object,
            _options.Object,
            NullLogger<AiJobProcessor>.Instance);

    private async Task<SeededJob> SeedRunningJobAsync(
        string jobType,
        string status = AiJobStatuses.Running,
        AiJobSource? source = null,
        Guid? projectId = null,
        Guid? userId = null)
    {
        var workerId = "worker-test";
        projectId ??= Guid.NewGuid();
        userId ??= Guid.NewGuid();
        var job = new AiJob
        {
            JobType = jobType,
            ProjectId = projectId.Value,
            SourceType = "manual",
            SchemaId = jobType + ".v4",
            SchemaVersion = "4.0",
            RequestJson = "{\"sourceText\":\"authorized input\",\"options\":null}",
            RequestHash = new string('a', 64),
            IdempotencyKey = $"processor:{Guid.NewGuid():N}",
            Status = status,
            StartedAt = DateTimeOffset.UtcNow,
            AvailableAt = DateTimeOffset.UtcNow,
            AttemptCount = 1,
            MaxAttempts = 3,
            CacheKey = $"processor:{Guid.NewGuid():N}",
            RequestedById = userId.Value
        };
        var dispatch = new AiJobDispatch
        {
            AiJobId = job.Id,
            AvailableAt = DateTimeOffset.UtcNow,
            LeaseOwner = workerId,
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
            DeliveryCount = 1
        };
        var attempt = new AiProviderAttempt
        {
            AiJobId = job.Id,
            AttemptNumber = 1,
            Status = AiAttemptStatuses.Running,
            ProviderName = "auto",
            ModelName = "pending"
        };
        source ??= new AiJobSource
        {
            SourceType = "manual",
            SourceHash = new string('b', 64),
            SortOrder = 0
        };
        source.AiJobId = job.Id;
        job.Sources.Add(source);
        _db.AiJobs.Add(job);
        _db.AiJobDispatches.Add(dispatch);
        _db.AiProviderAttempts.Add(attempt);
        await _db.SaveChangesAsync();
        return new SeededJob(
            job.Id,
            attempt.Id,
            workerId,
            new AiJobLease(dispatch.Id, job.Id, attempt.Id, 1, dispatch.LeaseExpiresAt!.Value));
    }

    private sealed record SeededJob(Guid JobId, Guid AttemptId, string WorkerId, AiJobLease Lease);
}
