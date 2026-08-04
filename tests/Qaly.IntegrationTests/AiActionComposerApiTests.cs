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
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.IntegrationTests;

public sealed class AiActionComposerApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid DefaultUserId = Guid.Parse("B0000000-0000-0000-0000-000000000000");
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AiActionComposerApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-01")]
    [Trait("TestId", "TEST-ACTION-07")]
    [Trait("TestId", "TEST-ACTION-09")]
    [Trait("TestId", "TEST-ACTION-12")]
    public async Task ComposeReviewConfirmReplay_PersistsTasksSkillsReceiptActivityAndAudit()
    {
        var scope = await SeedScopeAsync(includeViewer: false);
        var csrf = await GetCsrfTokenAsync(_client);
        var compose = await ComposeAsync(
            _client,
            scope.ProjectId,
            csrf,
            $"compose-{Guid.NewGuid():N}");
        compose.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(compose);

        var initialActivity = await GetResultAsync<AiActionActivityFeedDto>(
            _client,
            $"/api/ai/jobs/{created.JobId}/activity");
        initialActivity.Events.Should().Contain(item =>
            item.Stage == AiActionActivityStages.UnderstandIntent &&
            item.Status == AiActionActivityStatuses.Queued);

        var draftId = await CompleteActionJobAsync(created.JobId);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");
        var reviewed = draft.WorkingPayload.Deserialize<AiActionPlanDto>(JsonOptions)!;
        var option = reviewed.Options[0];
        var editedCommand = option.Commands[0] with
        {
            Title = "Reviewed native AI task",
            AssigneeId = DefaultUserId,
            AssigneeMode = "user_selected",
            Priority = "Critical"
        };
        reviewed = reviewed with
        {
            Options = [option with { Commands = [editedCommand] }],
            Review = new AiActionReviewSelectionDto(option.OptionId, [editedCommand.CommandId])
        };
        var reviewedJson = JsonSerializer.Serialize(reviewed, JsonOptions);
        var confirmKey = $"confirm-{Guid.NewGuid():N}";

        var confirmedResponse = await ConfirmAsync(
            _client,
            draftId,
            reviewedJson,
            draft.RowVersion,
            confirmKey,
            csrf);
        confirmedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await ReadResultAsync<AiDraftConfirmResultDto>(confirmedResponse);
        confirmed.CreatedTaskCount.Should().Be(1);
        confirmed.AppliedSkillCount.Should().Be(1);
        confirmed.ActionReceipt.Should().NotBeNull();
        confirmed.ActionReceipt!.Status.Should().Be("succeeded");
        confirmed.ActionReceipt.CommandResults.Should().ContainSingle(item =>
            item.Status == "succeeded" && item.EntityUrl != null);

        var replay = await ConfirmAsync(
            _client,
            draftId,
            reviewedJson,
            draft.RowVersion,
            confirmKey,
            csrf);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadResultAsync<AiDraftConfirmResultDto>(replay)).Should().BeEquivalentTo(confirmed);

        var activity = await GetResultAsync<AiActionActivityFeedDto>(
            _client,
            $"/api/ai/jobs/{created.JobId}/activity");
        activity.Events.Should().Contain(item =>
            item.Stage == AiActionActivityStages.ExecuteCommands &&
            item.Status == AiActionActivityStatuses.Succeeded);
        activity.Events.Should().Contain(item => item.Stage == AiActionActivityStages.PersistReceipt);
        activity.Events.Should().Contain(item => item.Stage == AiActionActivityStages.ReadBack);

        using var dbScope = _factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var taskId = confirmed.CreatedTaskIds.Single();
        var task = await db.TaskItems.AsNoTracking().SingleAsync(item => item.Id == taskId);
        task.Title.Should().Be("Reviewed native AI task");
        task.Priority.Should().Be("Critical");
        task.AssigneeId.Should().Be(DefaultUserId);
        (await db.TaskAssignments.CountAsync(item => item.TaskItemId == taskId)).Should().Be(1);
        (await db.TaskSkillRequirements.CountAsync(item =>
            item.TaskItemId == taskId &&
            item.OrganizationSkillId == scope.SkillId &&
            item.Provenance == TaskSkillService.ProvenanceAiConfirmed)).Should().Be(1);
        (await db.TaskItems.CountAsync(item => item.ProjectId == scope.ProjectId && item.Title == task.Title)).Should().Be(1);
        (await db.AuditLogs.CountAsync(item =>
            item.Action == "ConfirmAiDraft" && item.EntityId == draftId.ToString())).Should().Be(1);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-03")]
    public async Task ViewerAndForeignProject_ComposeAreDeniedWithoutCreatingJobs()
    {
        var scope = await SeedScopeAsync(includeViewer: true);
        var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.Add("X-Test-UserId", scope.ViewerId!.Value.ToString());
        var viewerCsrf = await GetCsrfTokenAsync(viewer);

        var viewerResponse = await ComposeAsync(
            viewer,
            scope.ProjectId,
            viewerCsrf,
            $"viewer-{Guid.NewGuid():N}");
        viewerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var foreignResponse = await ComposeAsync(
            _client,
            Guid.NewGuid(),
            await GetCsrfTokenAsync(_client),
            $"foreign-{Guid.NewGuid():N}");
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var dbScope = _factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AiJobs.CountAsync(item => item.ProjectId == scope.ProjectId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-ACTION-08")]
    public async Task SourceChangesAfterGeneration_ConfirmationFailsClosedWithoutMutation()
    {
        var scope = await SeedScopeAsync(includeViewer: false);
        var csrf = await GetCsrfTokenAsync(_client);
        var compose = await ComposeAsync(
            _client,
            scope.ProjectId,
            csrf,
            $"stale-compose-{Guid.NewGuid():N}");
        var created = await ReadResultAsync<AiJobCreatedDto>(compose);
        var draftId = await CompleteActionJobAsync(created.JobId);
        var draft = await GetResultAsync<AiDraftDetailDto>(_client, $"/api/ai/drafts/{draftId}");

        using (var updateScope = _factory.Services.CreateScope())
        {
            var db = updateScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var project = await db.Projects.SingleAsync(item => item.Id == scope.ProjectId);
            project.Name = $"Changed after generation {Guid.NewGuid():N}";
            project.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
            await db.SaveChangesAsync();
        }

        var response = await ConfirmAsync(
            _client,
            draftId,
            draft.WorkingPayload.GetRawText(),
            draft.RowVersion,
            $"stale-confirm-{Guid.NewGuid():N}",
            csrf);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadEnvelopeAsync<AiDraftConfirmResultDto>(response)).ErrorCode
            .Should().Be(AiErrorCodes.SourceStale);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await assertDb.TaskItems.CountAsync(item => item.ProjectId == scope.ProjectId)).Should().Be(0);
        (await assertDb.AiGeneratedDrafts.SingleAsync(item => item.Id == draftId)).Status
            .Should().Be(AiDraftStatuses.PendingReview);
    }

    private async Task<SeededScope> SeedScopeAsync(bool includeViewer)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        await EnsureUserAsync(db, DefaultUserId, "Action Composer Owner");
        Guid? viewerId = includeViewer ? Guid.NewGuid() : null;
        if (viewerId.HasValue) await EnsureUserAsync(db, viewerId.Value, "Read-only Viewer");

        var organization = new Organization
        {
            Name = $"Action Composer Org {Guid.NewGuid():N}",
            OwnerId = DefaultUserId
        };
        var project = new Project
        {
            Name = $"Action Composer Project {Guid.NewGuid():N}",
            Code = $"AC-{Guid.NewGuid():N}"[..12],
            OwnerId = DefaultUserId,
            OrganizationId = organization.Id,
            Status = "Active",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var skill = new OrganizationSkill
        {
            OrganizationId = organization.Id,
            Name = "Vue.js",
            NormalizedName = "vue.js",
            Description = "Frontend engineering",
            IsActive = true
        };
        db.AddRange(organization, project, skill);
        if (viewerId.HasValue)
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = viewerId.Value,
                Role = ProjectRoleRules.Viewer
            });
        }
        await db.SaveChangesAsync();
        return new SeededScope(organization.Id, project.Id, skill.Id, viewerId);
    }

    private async Task<Guid> CompleteActionJobAsync(Guid jobId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var job = await db.AiJobs.Include(item => item.Dispatch).SingleAsync(item => item.Id == jobId);
        using var request = JsonDocument.Parse(job.RequestJson);
        var snapshotJson = request.RootElement.GetProperty("sourceText").GetString()!;
        var snapshot = JsonSerializer.Deserialize<AiActionContextSnapshotDto>(snapshotJson, JsonOptions)!;
        var skill = snapshot.Skills.Single();
        var command = new AiActionTaskCommandDto(
            "task-1",
            AiActionComposerContract.TaskCreateTool,
            "1.0",
            "AI proposed task",
            "Draft only until a human confirms.",
            ["Authorized happy path works", "Permission denial is tested"],
            "High",
            DateTimeOffset.UtcNow.AddDays(7),
            8,
            null,
            "unassigned",
            [new AiActionSkillSelectionDto(skill.SkillId, "Proficient")],
            [snapshot.Project.SourceRef, skill.SourceRef]);
        var model = new AiActionPlanDto(
            AiActionComposerContract.SchemaId,
            "1.0",
            snapshot.Project.Id,
            snapshot.SourceVersion,
            snapshot.UserIntent,
            "task.create",
            0.91m,
            [],
            [],
            [],
            [],
            [new AiActionOptionDto("balanced", "Cân bằng", "Một task có thể duyệt.", [], [command])],
            new AiActionReviewSelectionDto("ignored", []),
            DateTimeOffset.UtcNow);
        AiActionComposerOutputContract.TryBuildResult(
            JsonSerializer.Serialize(model, JsonOptions),
            snapshotJson,
            out var resultJson,
            out var error).Should().BeTrue(error);

        job.Status = AiJobStatuses.Succeeded;
        job.ProgressPercent = 100;
        job.FinishedAt = DateTimeOffset.UtcNow;
        job.ResultJson = resultJson;
        job.ResultHash = Hash(resultJson);
        job.SelectedProvider = "DeepSeek";
        job.SelectedModel = "deepseek-v4-pro";
        if (job.Dispatch != null)
        {
            job.Dispatch.CompletedAt = DateTimeOffset.UtcNow;
            job.Dispatch.LeaseOwner = null;
            job.Dispatch.LeaseExpiresAt = null;
        }
        var draft = new AiGeneratedDraft
        {
            AiJobId = job.Id,
            ProjectId = job.ProjectId!.Value,
            DraftType = AiActionComposerContract.DraftType,
            PayloadJson = resultJson,
            OriginalPayloadJson = resultJson,
            WorkingPayloadJson = resultJson,
            Status = AiDraftStatuses.PendingReview,
            SchemaId = AiActionComposerContract.SchemaId,
            SourceHashAtGeneration = job.RequestHash,
            RowVersion = [7, 8, 9]
        };
        db.AiGeneratedDrafts.Add(draft);
        await db.SaveChangesAsync();
        return draft.Id;
    }

    private static async Task<HttpResponseMessage> ComposeAsync(
        HttpClient client,
        Guid projectId,
        string csrf,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/ai/actions/compose")
        {
            Content = JsonContent.Create(new AiActionComposeRequestDto(
                "Tạo một task frontend có skill và tiêu chí nghiệm thu.",
                new AiActionClientContextDto("/projects/test", "project_tasks", projectId, "project", projectId)))
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client,
        Guid draftId,
        string payload,
        string rowVersion,
        string idempotencyKey,
        string csrf)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/ai/drafts/{draftId}/confirm")
        {
            Content = JsonContent.Create(new ConfirmAiDraftDto(
                payload,
                AiActionComposerContract.ConfirmAction,
                "Reviewed by integration test",
                rowVersion,
                idempotencyKey))
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(message);
    }

    private static async Task EnsureUserAsync(QalyDbContext db, Guid userId, string name)
    {
        if (await db.Users.AnyAsync(item => item.Id == userId)) return;
        db.Users.Add(new User
        {
            Id = userId,
            FullName = name,
            Email = $"{userId:N}@action-composer.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private static async Task<T> GetResultAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadResultAsync<T>(response);
    }

    private static async Task<T> ReadResultAsync<T>(HttpResponseMessage response)
    {
        var envelope = await ReadEnvelopeAsync<T>(response);
        envelope.IsSuccess.Should().BeTrue(envelope.Error);
        envelope.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    private static async Task<ApiEnvelope<T>> ReadEnvelopeAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions))!;

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
        => (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record SeededScope(Guid OrganizationId, Guid ProjectId, Guid SkillId, Guid? ViewerId);
    private sealed record CsrfResponse(string Token);
    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error, string? ErrorCode, int StatusCode);
}
