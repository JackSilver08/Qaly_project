using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Globalization;
using System.Text.Json;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
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
    private readonly Mock<IAiCostService> _costService = new();
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

    [Fact]
    public async Task ProcessAsync_ProjectProgressSummary_MergesOnlyGroundedNarrativeAndCreatesNoDraft()
    {
        var seeded = await SeedRunningJobAsync("project_progress_summary");
        await ConfigureProgressJobAsync(seeded.JobId, total: 4, done: 2, inProgress: 1, todo: 1);
        AiRequest? captured = null;
        _gateway.Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = true,
                ProviderName = "test-provider",
                ModelName = "grounded-model",
                Content = """
                    {
                      "summaryPoints":[{"text":"Hai trên bốn task đã hoàn thành.","metricRefs":["done","total"],"sourceRefs":["project:11111111-1111-1111-1111-111111111111"]}],
                      "risks":[{"code":"OVERDUE","severity":"high","title":"Có task quá hạn.","metricRefs":["overdue"],"sourceRefs":["task:22222222-2222-2222-2222-222222222222"]}],
                      "nextActions":[{"title":"Rà soát task quá hạn","rationale":"Giảm điểm nghẽn hiện hữu.","metricRefs":["overdue"],"sourceRefs":["task:22222222-2222-2222-2222-222222222222"]}]
                    }
                    """
            });

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.SingleAsync(item => item.Id == seeded.JobId);
        job.Status.Should().Be(AiJobStatuses.Succeeded);
        job.SchemaId.Should().Be("progress_summary.v4");
        using var result = JsonDocument.Parse(job.ResultJson!);
        result.RootElement.GetProperty("metrics").GetProperty("total").GetInt32().Should().Be(4);
        result.RootElement.GetProperty("summaryPoints")[0].GetProperty("text").GetString()
            .Should().Be("Hai trên bốn task đã hoàn thành.");
        result.RootElement.TryGetProperty("taskFacts", out _).Should().BeFalse();
        (await _db.AiGeneratedDrafts.CountAsync(item => item.AiJobId == job.Id)).Should().Be(0);
        captured.Should().NotBeNull();
        captured!.ValidationContextJson.Should().NotBeNullOrWhiteSpace();
        captured.UseRetrievalAugmentation.Should().BeFalse();
        captured.AllowMockFallback.Should().BeFalse();
        captured.Prompt.Should().Contain("Authorized server snapshot");
        _compliance.Verify(service => service.LogJobAuditEventAsync(
            It.IsAny<Guid?>(),
            job.ProjectId,
            It.IsAny<Guid?>(),
            "AI_JOB_SUCCEEDED",
            nameof(AiJob),
            null,
            null,
            It.Is<string?>(json => json != null && json.Contains(job.Id.ToString(), StringComparison.Ordinal)),
            job.Id,
            job.Id,
            seeded.AttemptId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_EmptyProjectProgress_CompletesDeterministicallyWithoutProviderOrDraft()
    {
        var seeded = await SeedRunningJobAsync("project_progress_summary");
        await ConfigureProgressJobAsync(seeded.JobId, total: 0, done: 0, inProgress: 0, todo: 0);

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.SingleAsync(item => item.Id == seeded.JobId);
        job.Status.Should().Be(AiJobStatuses.Succeeded);
        job.SelectedProvider.Should().Be("deterministic");
        job.IsMock.Should().BeFalse();
        using var result = JsonDocument.Parse(job.ResultJson!);
        result.RootElement.GetProperty("warnings")[0].GetString().Should().Be("NO_PROGRESS_TASKS");
        result.RootElement.GetProperty("summaryPoints").GetArrayLength().Should().Be(0);
        _gateway.Verify(
            gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _costService.Verify(
            cost => cost.RecordJobUsageAsync(
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                "project_progress_summary",
                "deterministic",
                "server-owned-empty-v1",
                0,
                0,
                0m,
                It.IsAny<int?>(),
                "success",
                false,
                job.Id,
                seeded.AttemptId,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        (await _db.AiGeneratedDrafts.CountAsync(item => item.AiJobId == job.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_EmptySprintProgress_CompletesDeterministicallyWithoutProviderOrDraft()
    {
        var sprintId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var seeded = await SeedRunningJobAsync("sprint_progress_summary");
        await ConfigureProgressJobAsync(
            seeded.JobId,
            total: 0,
            done: 0,
            inProgress: 0,
            todo: 0,
            sprintId: sprintId);

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.SingleAsync(item => item.Id == seeded.JobId);
        job.Status.Should().Be(AiJobStatuses.Succeeded);
        job.SelectedProvider.Should().Be("deterministic");
        using var result = JsonDocument.Parse(job.ResultJson!);
        result.RootElement.GetProperty("scope").GetProperty("type").GetString().Should().Be("sprint");
        result.RootElement.GetProperty("scope").GetProperty("sprintId").GetGuid().Should().Be(sprintId);
        result.RootElement.GetProperty("warnings")[0].GetString().Should().Be("NO_PROGRESS_TASKS");
        _gateway.Verify(
            gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _costService.Verify(
            cost => cost.RecordJobUsageAsync(
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                "sprint_progress_summary",
                "deterministic",
                "server-owned-empty-v1",
                0,
                0,
                0m,
                It.IsAny<int?>(),
                "success",
                false,
                job.Id,
                seeded.AttemptId,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        (await _db.AiGeneratedDrafts.CountAsync(item => item.AiJobId == job.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-03")]
    [Trait("TestId", "TEST-SKILL-11")]
    public async Task ProcessAsync_TaskSkillSuggestion_ReconcilesAndPersistsReviewDraftAndUsage()
    {
        var seeded = await SeedRunningJobAsync(TaskSkillAiContract.JobType);
        var context = await ConfigureTaskSkillJobAsync(seeded.JobId);
        AiRequest? captured = null;
        var providerPayload = JsonSerializer.Serialize(new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            context.TaskId,
            context.SourceVersion,
            "ready",
            [
                new TaskSkillSuggestionItemDto(
                    context.SkillId,
                    "Model attempted rename",
                    "proficient",
                    0.876m,
                    "The authorized task explicitly requires a Vue component.",
                    [context.SourceRef])
            ],
            [],
            DateTimeOffset.UtcNow));
        _gateway.Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = true,
                Content = providerPayload,
                ProviderName = "test-provider",
                ModelName = "task-skill-model",
                InputTokens = 40,
                OutputTokens = 20,
                EstimatedCostUsd = 0.002m
            });

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.SingleAsync(item => item.Id == seeded.JobId);
        job.Status.Should().Be(AiJobStatuses.Succeeded);
        job.IsMock.Should().BeFalse();
        using var result = JsonDocument.Parse(job.ResultJson!);
        var suggestion = result.RootElement.GetProperty("suggestions")[0];
        suggestion.GetProperty("canonicalName").GetString().Should().Be("Vue.js");
        suggestion.GetProperty("requiredLevel").GetString().Should().Be("Proficient");
        suggestion.GetProperty("confidence").GetDecimal().Should().Be(0.88m);
        var draft = await _db.AiGeneratedDrafts.SingleAsync(item => item.AiJobId == job.Id);
        draft.DraftType.Should().Be(TaskSkillAiContract.DraftType);
        draft.SchemaId.Should().Be(TaskSkillAiContract.SchemaId);
        draft.Status.Should().Be(AiDraftStatuses.PendingReview);
        captured.Should().NotBeNull();
        captured!.ValidationContextJson.Should().Contain(TaskSkillAiContract.SnapshotSchemaId);
        captured.UseRetrievalAugmentation.Should().BeFalse();
        captured.AllowMockFallback.Should().BeFalse();
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-04")]
    public async Task ProcessAsync_EmptyTaskSkillCatalog_CompletesDeterministicallyWithReviewableEmptyDraft()
    {
        var seeded = await SeedRunningJobAsync(TaskSkillAiContract.JobType);
        await ConfigureTaskSkillJobAsync(seeded.JobId, includeSkill: false);

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.SingleAsync(item => item.Id == seeded.JobId);
        job.Status.Should().Be(AiJobStatuses.Succeeded);
        job.SelectedProvider.Should().Be("deterministic");
        job.IsMock.Should().BeFalse();
        using var result = JsonDocument.Parse(job.ResultJson!);
        result.RootElement.GetProperty("dataState").GetString().Should().Be("empty");
        result.RootElement.GetProperty("suggestions").GetArrayLength().Should().Be(0);
        (await _db.AiGeneratedDrafts.CountAsync(item => item.AiJobId == job.Id)).Should().Be(1);
        _gateway.Verify(
            gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _costService.Verify(service => service.RecordJobUsageAsync(
            It.IsAny<Guid?>(),
            job.ProjectId,
            job.RequestedById,
            TaskSkillAiContract.JobType,
            "deterministic",
            "server-owned-empty-v1",
            0,
            0,
            0m,
            It.IsAny<int?>(),
            "success",
            false,
            job.Id,
            seeded.AttemptId,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-06")]
    public async Task ProcessAsync_TaskSkillProviderUnavailable_RetriesWithoutDraftOrFakeResult()
    {
        var seeded = await SeedRunningJobAsync(TaskSkillAiContract.JobType);
        await ConfigureTaskSkillJobAsync(seeded.JobId);
        _gateway.Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = false,
                ErrorCode = AiErrorCodes.ProviderUnavailable,
                ErrorMessage = "provider timeout",
                Retryable = true
            });

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.SingleAsync(item => item.Id == seeded.JobId);
        job.Status.Should().Be(AiJobStatuses.Retrying);
        job.LastErrorCode.Should().Be(AiErrorCodes.ProviderUnavailable);
        job.ResultJson.Should().BeNull();
        (await _db.AiGeneratedDrafts.CountAsync(item => item.AiJobId == job.Id)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-SKILL-07")]
    public async Task ProcessAsync_TaskSkillInvalidSemanticSchema_FailsTerminalWithoutDraft()
    {
        var seeded = await SeedRunningJobAsync(TaskSkillAiContract.JobType);
        var context = await ConfigureTaskSkillJobAsync(seeded.JobId);
        var foreignSkillId = Guid.NewGuid();
        var invalid = JsonSerializer.Serialize(new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            context.TaskId,
            context.SourceVersion,
            "ready",
            [
                new TaskSkillSuggestionItemDto(
                    foreignSkillId,
                    "Invented",
                    "SuperExpert",
                    1.5m,
                    "Ungrounded suggestion.",
                    [])
            ],
            [],
            DateTimeOffset.UtcNow));
        _gateway.Setup(gateway => gateway.ExecuteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                IsSuccess = true,
                Content = invalid,
                ProviderName = "test-provider",
                ModelName = "invalid-model"
            });

        await CreateProcessor().ProcessAsync(seeded.Lease, seeded.WorkerId);

        var job = await _db.AiJobs.SingleAsync(item => item.Id == seeded.JobId);
        job.Status.Should().Be(AiJobStatuses.Failed);
        job.LastErrorCode.Should().Be(AiErrorCodes.SchemaInvalid);
        job.LastErrorRetryable.Should().BeFalse();
        job.ResultJson.Should().BeNull();
        (await _db.AiGeneratedDrafts.CountAsync(item => item.AiJobId == job.Id)).Should().Be(0);
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
            NullLogger<AiJobProcessor>.Instance,
            _costService.Object);

    private async Task ConfigureProgressJobAsync(
        Guid jobId,
        int total,
        int done,
        int inProgress,
        int todo,
        Guid? sprintId = null)
    {
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var taskId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        object scope = sprintId.HasValue
            ? new
            {
                type = "sprint",
                projectId,
                projectName = "Alpha",
                projectCode = "ALP",
                sprintId = sprintId.Value,
                sprintName = "Release milestone"
            }
            : new { projectId, projectName = "Alpha", projectCode = "ALP" };
        object scopeSource = sprintId.HasValue
            ? new
            {
                key = $"sprint:{sprintId.Value:D}",
                type = "sprint",
                entityId = sprintId.Value,
                label = "Release milestone",
                url = $"/projects/{projectId:D}#milestone-{sprintId.Value:D}",
                version = "v1"
            }
            : new
            {
                key = $"project:{projectId:D}",
                type = "project",
                entityId = projectId,
                label = "Alpha",
                url = $"/projects/{projectId:D}",
                version = "v1"
            };
        var snapshot = JsonSerializer.Serialize(new
        {
            snapshotVersion = sprintId.HasValue
                ? "sprint_progress_snapshot.v1"
                : "project_progress_snapshot.v1",
            scope,
            period = new
            {
                kind = "current_snapshot",
                snapshotAt = DateTimeOffset.Parse("2026-07-27T10:00:00Z", CultureInfo.InvariantCulture)
            },
            coverage = new
            {
                dataState = total == 0 ? "empty" : "sufficient",
                visibility = "manager_full_project",
                includedTaskCount = total,
                excludedTaskCount = 0
            },
            metrics = new
            {
                total,
                done,
                inProgress,
                todo,
                overdue = total == 0 ? 0 : 1,
                dueSoon = 0,
                completionRate = total == 0 ? 0m : Math.Round(done * 100m / total, 2)
            },
            sourceRefs = new object[]
            {
                scopeSource,
                new
                {
                    key = $"task:{taskId:D}",
                    type = "task",
                    entityId = taskId,
                    label = "Risk task",
                    url = $"/projects/{projectId:D}/tasks/{taskId:D}",
                    version = "v1"
                }
            },
            taskFacts = Array.Empty<object>()
        });
        var job = await _db.AiJobs.Include(item => item.Sources).SingleAsync(item => item.Id == jobId);
        job.ProjectId = projectId;
        job.SourceType = sprintId.HasValue ? "sprint" : "project";
        job.SchemaId = "progress_summary.v4";
        job.RequestJson = JsonSerializer.Serialize(new
        {
            sourceText = snapshot,
            cacheMode = "use",
            options = new { prompt = "Return grounded narrative.", systemPrompt = "Return JSON only." }
        });
        job.Sources.Single().SourceType = sprintId.HasValue ? "sprint" : "project";
        job.Sources.Single().SourceEntityId = sprintId ?? projectId;
        await _db.SaveChangesAsync();
    }

    private async Task<TaskSkillProcessorContext> ConfigureTaskSkillJobAsync(
        Guid jobId,
        bool includeSkill = true)
    {
        var taskId = Guid.Parse("61111111-1111-1111-1111-111111111111");
        var projectId = Guid.Parse("62222222-2222-2222-2222-222222222222");
        var organizationId = Guid.Parse("63333333-3333-3333-3333-333333333333");
        var skillId = Guid.Parse("64444444-4444-4444-4444-444444444444");
        var sourceRef = $"task:{taskId:D}";
        const string sourceVersion = "task-skill-source-v1";
        var skills = includeSkill
            ? new[] { new TaskSkillCatalogItemDto(skillId, "Vue.js", "Frontend components") }
            : [];
        var snapshot = JsonSerializer.Serialize(new TaskSkillSuggestionSnapshotDto(
            TaskSkillAiContract.SnapshotSchemaId,
            new TaskSkillSuggestionTaskContextDto(
                taskId,
                projectId,
                organizationId,
                "task-row-v1",
                "Build Vue task card",
                "Implement an authorized native review component.",
                "High",
                false,
                sourceRef),
            sourceVersion,
            includeSkill ? "catalog-v1" : "catalog-empty-v1",
            "vi",
            skills));
        var job = await _db.AiJobs.Include(item => item.Sources).SingleAsync(item => item.Id == jobId);
        if (!await _db.Organizations.AnyAsync(item => item.Id == organizationId))
        {
            _db.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = "Task Skill Organization",
                Code = "AI-SKILL",
                OwnerId = job.RequestedById,
                IsActive = true
            });
        }
        if (!await _db.Projects.AnyAsync(item => item.Id == projectId))
        {
            _db.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Task Skill Project",
                Code = "AI-SKILL",
                OwnerId = job.RequestedById,
                OrganizationId = organizationId
            });
        }
        job.ProjectId = projectId;
        job.TenantId = organizationId;
        job.SourceType = "task";
        job.SourceId = taskId.ToString("D");
        job.SchemaId = TaskSkillAiContract.SchemaId;
        job.SchemaVersion = "1.0";
        job.RequestJson = JsonSerializer.Serialize(new
        {
            sourceText = snapshot,
            cacheMode = "use",
            options = new
            {
                prompt = "Select only canonical skills.",
                systemPrompt = $"Return {TaskSkillAiContract.SchemaId} JSON only."
            }
        });
        var source = job.Sources.Single();
        source.SourceType = "task";
        source.SourceEntityId = taskId;
        await _db.SaveChangesAsync();
        return new TaskSkillProcessorContext(taskId, skillId, sourceVersion, sourceRef);
    }

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
        if (!await _db.Users.AnyAsync(item => item.Id == userId.Value))
        {
            _db.Users.Add(new User
            {
                Id = userId.Value,
                FullName = "AI Job Owner",
                Email = $"{userId.Value:N}@qaly.test",
                PasswordHash = "not-used"
            });
        }
        if (!await _db.Projects.AnyAsync(item => item.Id == projectId.Value))
        {
            _db.Projects.Add(new Project
            {
                Id = projectId.Value,
                Name = "AI Job Project",
                Code = $"AI-{projectId.Value:N}"[..12],
                OwnerId = userId.Value
            });
        }
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
    private sealed record TaskSkillProcessorContext(
        Guid TaskId,
        Guid SkillId,
        string SourceVersion,
        string SourceRef);
}
