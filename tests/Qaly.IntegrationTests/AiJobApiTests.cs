using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiJobApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AiJobApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateJob_WithSameIdempotencyKey_ReplaysAndRejectsDifferentPayload()
    {
        var projectId = await SeedProjectAsync();
        var csrf = await GetCsrfTokenAsync(_client);
        const string idempotencyKey = "ai-api-idempotency-1";
        var request = CreateRequest(projectId, "Create a release task");

        var first = await PostJobAsync(_client, request, idempotencyKey, csrf);
        var replay = await PostJobAsync(_client, request, idempotencyKey, csrf);
        var conflict = await PostJobAsync(_client, CreateRequest(projectId, "Different source"), idempotencyKey, csrf);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        replay.StatusCode.Should().Be(HttpStatusCode.Accepted);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var firstPayload = await first.Content.ReadFromJsonAsync<ApiResult<AiJobCreatedDto>>(JsonOptions);
        var replayPayload = await replay.Content.ReadFromJsonAsync<ApiResult<AiJobCreatedDto>>(JsonOptions);
        firstPayload!.Data!.JobId.Should().Be(replayPayload!.Data!.JobId);
        var conflictPayload = await conflict.Content.ReadFromJsonAsync<ApiResult<object>>(JsonOptions);
        conflictPayload!.ErrorCode.Should().Be("AI_IDEMPOTENCY_CONFLICT");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AiJobs.CountAsync(job => job.IdempotencyKey == idempotencyKey)).Should().Be(1);
        (await db.AiJobDispatches.CountAsync(item => item.AiJobId == firstPayload.Data.JobId)).Should().Be(1);
        (await db.AiJobSources.CountAsync(item => item.AiJobId == firstPayload.Data.JobId)).Should().Be(1);
    }

    [Fact]
    public async Task JobStatus_VisibilityAndCancel_ArePermissionAwareAndIdempotent()
    {
        var projectId = await SeedProjectAsync();
        var csrf = await GetCsrfTokenAsync(_client);
        var response = await PostJobAsync(_client, CreateRequest(projectId, "Cancelable request"), "ai-api-cancel-1", csrf);
        var accepted = await response.Content.ReadFromJsonAsync<ApiResult<AiJobCreatedDto>>(JsonOptions);
        var jobId = accepted!.Data!.JobId;

        var status = await _client.GetAsync($"/api/ai/jobs/{jobId}");
        status.StatusCode.Should().Be(HttpStatusCode.OK);

        var cancelRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/ai/jobs/{jobId}/cancel")
        {
            Content = JsonContent.Create(new CancelAiJobDto("user canceled"))
        };
        cancelRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        var canceled = await _client.SendAsync(cancelRequest);
        canceled.StatusCode.Should().Be(HttpStatusCode.OK);
        var canceledPayload = await canceled.Content.ReadFromJsonAsync<ApiResult<AiJobDetailDto>>(JsonOptions);
        canceledPayload!.Data!.Status.Should().Be(AiJobStatuses.Canceled);

        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());
        var hidden = await otherClient.GetAsync($"/api/ai/jobs/{jobId}");
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConfirmDraft_RequiresCsrfIdempotencyAndCurrentRowVersion_ThenMutatesOnce()
    {
        var (projectId, draftId) = await SeedPendingTaskDraftAsync();
        var detailResponse = await _client.GetAsync($"/api/ai/drafts/{draftId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResult<AiDraftDetailDto>>(JsonOptions);
        detail!.Data!.RowVersion.Should().NotBeNull();

        var withoutCsrf = await _client.PostAsJsonAsync(
            $"/api/ai/drafts/{draftId}/confirm",
            new ConfirmAiDraftDto(null, "create_tasks", "approved", detail.Data.RowVersion, "confirm-1"));
        withoutCsrf.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var csrf = await GetCsrfTokenAsync(_client);
        var stale = await _client.SendAsync(CreateConfirmRequest(draftId, "AQIDBA==", csrf, "confirm-stale"));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var stalePayload = await stale.Content.ReadFromJsonAsync<ApiResult<AiDraftConfirmResultDto>>(JsonOptions);
        stalePayload!.ErrorCode.Should().Be(AiErrorCodes.DraftConcurrencyConflict);

        var confirmMessage = CreateConfirmRequest(draftId, detail.Data.RowVersion, csrf, "confirm-1");
        var confirmed = await _client.SendAsync(confirmMessage);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmedPayload = await confirmed.Content.ReadFromJsonAsync<ApiResult<AiDraftConfirmResultDto>>(JsonOptions);
        confirmedPayload!.Data!.CreatedTaskCount.Should().Be(1);

        var replay = await _client.SendAsync(CreateConfirmRequest(draftId, detail.Data.RowVersion, csrf, "confirm-1"));
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayPayload = await replay.Content.ReadFromJsonAsync<ApiResult<AiDraftConfirmResultDto>>(JsonOptions);
        replayPayload!.Data!.CreatedTaskIds.Should().Equal(confirmedPayload.Data.CreatedTaskIds);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskItems.CountAsync(task => task.ProjectId == projectId)).Should().Be(1);
    }

    [Fact]
    public async Task TaskDraftWrapper_WithFeatureEnabled_EnqueuesCanonicalJob()
    {
        var projectId = await SeedProjectAsync();
        var csrf = await GetCsrfTokenAsync(_client);
        const string sourceText = "Create a task draft from this authorized note.";
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ai/task-drafts/from-source")
        {
            Content = JsonContent.Create(new AiFunctionJobRequest(
                ProjectId: projectId,
                SourceType: "manual",
                SourceHash: Hash(sourceText),
                SourceText: sourceText))
        };
        message.Headers.Add("Idempotency-Key", "ai-wrapper-task-draft-1");
        message.Headers.Add("X-CSRF-TOKEN", csrf);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<AiJobCreatedDto>>(JsonOptions);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var job = await db.AiJobs.Include(item => item.Dispatch).Include(item => item.Sources)
            .SingleAsync(item => item.Id == payload!.Data!.JobId);
        job.JobType.Should().Be("task_draft");
        job.SchemaId.Should().Be("task_draft.v4");
        job.Status.Should().Be(AiJobStatuses.Queued);
        job.Dispatch.Should().NotBeNull();
        job.Sources.Should().ContainSingle(source => source.SourceType == "manual" && source.SourceHash == Hash(sourceText));
    }

    [Fact]
    public async Task JobListResultAndRetry_ExposeCanonicalLifecycle()
    {
        var (projectId, succeededJobId, failedJobId) = await SeedResultAndRetryJobsAsync();

        var listResponse = await _client.GetAsync($"/api/ai/jobs?projectId={projectId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResult<IReadOnlyList<AiJobSummaryDto>>>(JsonOptions);
        list!.Data.Should().Contain(item => item.JobId == succeededJobId && item.Status == AiJobStatuses.Succeeded);
        list.Data.Should().Contain(item => item.JobId == failedJobId && item.Status == AiJobStatuses.Failed);

        var resultResponse = await _client.GetAsync($"/api/ai/jobs/{succeededJobId}/result");
        resultResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resultResponse.Content.ReadFromJsonAsync<ApiResult<AiJobResultDto>>(JsonOptions);
        result!.Data!.Result.GetProperty("summary").GetString().Should().Be("Canonical result");

        var unavailable = await _client.GetAsync($"/api/ai/jobs/{failedJobId}/result");
        unavailable.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var unavailablePayload = await unavailable.Content.ReadFromJsonAsync<ApiResult<AiJobResultDto>>(JsonOptions);
        unavailablePayload!.ErrorCode.Should().Be(AiErrorCodes.ProviderUnavailable);

        var csrf = await GetCsrfTokenAsync(_client);
        var retryMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/ai/jobs/{failedJobId}/retry")
        {
            Content = JsonContent.Create(new RetryAiJobDto())
        };
        retryMessage.Headers.Add("X-CSRF-TOKEN", csrf);
        var retryResponse = await _client.SendAsync(retryMessage);
        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retried = await retryResponse.Content.ReadFromJsonAsync<ApiResult<AiJobDetailDto>>(JsonOptions);
        retried!.Data!.Status.Should().Be(AiJobStatuses.Retrying);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var dispatch = await db.AiJobDispatches.SingleAsync(item => item.AiJobId == failedJobId);
        dispatch.CompletedAt.Should().BeNull();
        dispatch.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task DraftPatchAndReject_RequireCsrfAndPersistReviewDecision()
    {
        var (projectId, draftId) = await SeedPendingTaskDraftAsync();
        var detailResponse = await _client.GetAsync($"/api/ai/drafts/{draftId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiResult<AiDraftDetailDto>>(JsonOptions);
        var csrf = await GetCsrfTokenAsync(_client);
        var editedPayload = JsonSerializer.Serialize(new AiTaskDraftPayload(
        [
            new AiTaskDraftItem("Ship canonical worker", "Edited during review")
        ]));

        var patchMessage = new HttpRequestMessage(HttpMethod.Patch, $"/api/ai/drafts/{draftId}")
        {
            Content = JsonContent.Create(new PatchAiDraftDto(editedPayload, detail!.Data!.RowVersion))
        };
        patchMessage.Headers.Add("X-CSRF-TOKEN", csrf);
        var patchResponse = await _client.SendAsync(patchMessage);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await patchResponse.Content.ReadFromJsonAsync<ApiResult<AiDraftDetailDto>>(JsonOptions);
        patched!.Data!.WorkingPayload.GetRawText().Should().Contain("Edited during review");

        var rejectMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/ai/drafts/{draftId}/reject")
        {
            Content = JsonContent.Create(new RejectAiDraftDto("Not suitable", patched.Data.RowVersion, "reject-draft-1"))
        };
        rejectMessage.Headers.Add("Idempotency-Key", "reject-draft-1");
        rejectMessage.Headers.Add("X-CSRF-TOKEN", csrf);
        var rejectResponse = await _client.SendAsync(rejectMessage);
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<ApiResult<AiDraftDetailDto>>(JsonOptions);
        rejected!.Data!.Status.Should().Be(AiDraftStatuses.Rejected);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskItems.CountAsync(item => item.ProjectId == projectId)).Should().Be(0);
        var storedDraft = await db.AiGeneratedDrafts.SingleAsync(item => item.Id == draftId);
        storedDraft.RejectionReason.Should().Be("Not suitable");
        storedDraft.ConfirmationIdempotencyKey.Should().Be("reject-draft-1");
    }

    private async Task<Guid> SeedProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == _factory.TestUserId);
        if (user == null)
        {
            user = new User
            {
                Id = _factory.TestUserId,
                FullName = "AI API Test",
                Email = "ai-api@qaly.test",
                PasswordHash = "not-used",
                Role = "Member"
            };
            db.Users.Add(user);
        }

        var project = new Project
        {
            Name = "AI API Project",
            Code = $"AI-{Guid.NewGuid():N}"[..12],
            OwnerId = user.Id
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<(Guid ProjectId, Guid DraftId)> SeedPendingTaskDraftAsync()
    {
        var projectId = await SeedProjectAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var payload = JsonSerializer.Serialize(new AiTaskDraftPayload(
        [
            new AiTaskDraftItem("Ship canonical worker", "Verified only after tests")
        ]));
        var job = new AiJob
        {
            JobType = "task_draft",
            ProjectId = projectId,
            SourceType = "manual",
            SchemaId = "task_draft.v4",
            SchemaVersion = "4.0",
            RequestJson = "{}",
            RequestHash = Hash(payload),
            IdempotencyKey = $"seed:{Guid.NewGuid():N}",
            Status = AiJobStatuses.Succeeded,
            ProgressPercent = 100,
            AvailableAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
            ResultJson = payload,
            ResultHash = Hash(payload),
            CacheKey = $"seed:{Guid.NewGuid():N}",
            RequestedById = _factory.TestUserId
        };
        var draft = new AiGeneratedDraft
        {
            AiJobId = job.Id,
            ProjectId = projectId,
            DraftType = "TaskDraft",
            PayloadJson = payload,
            OriginalPayloadJson = payload,
            WorkingPayloadJson = payload,
            Status = AiDraftStatuses.PendingReview,
            RowVersion = [1, 2, 3]
        };
        job.Sources.Add(new AiJobSource
        {
            AiJobId = job.Id,
            SourceType = "manual",
            SourceHash = Hash(payload),
            SortOrder = 0
        });
        db.AiJobs.Add(job);
        db.AiGeneratedDrafts.Add(draft);
        await db.SaveChangesAsync();
        return (projectId, draft.Id);
    }

    private async Task<(Guid ProjectId, Guid SucceededJobId, Guid FailedJobId)> SeedResultAndRetryJobsAsync()
    {
        var projectId = await SeedProjectAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        const string resultJson = "{\"summary\":\"Canonical result\"}";
        var succeeded = CreateSeedJob(projectId, AiJobStatuses.Succeeded);
        succeeded.ProgressPercent = 100;
        succeeded.ResultJson = resultJson;
        succeeded.ResultHash = Hash(resultJson);
        succeeded.FinishedAt = DateTimeOffset.UtcNow;
        var failed = CreateSeedJob(projectId, AiJobStatuses.Failed);
        failed.AttemptCount = 1;
        failed.LastErrorCode = AiErrorCodes.ProviderUnavailable;
        failed.LastErrorMessage = "provider unavailable";
        failed.LastErrorRetryable = true;
        failed.FinishedAt = DateTimeOffset.UtcNow;
        var failedDispatch = new AiJobDispatch
        {
            AiJobId = failed.Id,
            AvailableAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        db.AiJobs.AddRange(succeeded, failed);
        db.AiJobDispatches.Add(failedDispatch);
        await db.SaveChangesAsync();
        return (projectId, succeeded.Id, failed.Id);
    }

    private AiJob CreateSeedJob(Guid projectId, string status)
    {
        var sourceHash = Hash($"{projectId}:{status}:{Guid.NewGuid():N}");
        var job = new AiJob
        {
            JobType = "progress_summary",
            ProjectId = projectId,
            SourceType = "manual",
            SchemaId = "progress_summary.v4",
            SchemaVersion = "4.0",
            RequestJson = "{}",
            RequestHash = sourceHash,
            IdempotencyKey = $"seed:{Guid.NewGuid():N}",
            Status = status,
            AvailableAt = DateTimeOffset.UtcNow,
            MaxAttempts = 3,
            CacheKey = $"seed:{Guid.NewGuid():N}",
            RequestedById = _factory.TestUserId
        };
        job.Sources.Add(new AiJobSource
        {
            AiJobId = job.Id,
            SourceType = "manual",
            SourceHash = sourceHash,
            SortOrder = 0
        });
        return job;
    }

    private static CreateAiJobDto CreateRequest(Guid projectId, string sourceText)
        => new(
            "task_draft",
            projectId,
            "manual",
            null,
            SourceText: sourceText,
            SchemaId: "task_draft.v4",
            SourceHash: Hash(sourceText));

    private static async Task<HttpResponseMessage> PostJobAsync(
        HttpClient client,
        CreateAiJobDto request,
        string idempotencyKey,
        string csrf)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ai/jobs")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static HttpRequestMessage CreateConfirmRequest(Guid draftId, string rowVersion, string csrf, string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/ai/drafts/{draftId}/confirm")
        {
            Content = JsonContent.Create(new ConfirmAiDraftDto(null, "create_tasks", "approved", rowVersion, idempotencyKey))
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return message;
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var payload = await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions);
        return payload!.Token;
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record CsrfResponse(string Token);
    private sealed record ApiResult<T>(bool IsSuccess, T? Data, string? Error, string? ErrorCode, int StatusCode);
}
