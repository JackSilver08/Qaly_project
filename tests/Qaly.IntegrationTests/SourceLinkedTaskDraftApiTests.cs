using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class SourceLinkedTaskDraftApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;

    public SourceLinkedTaskDraftApiTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    [Trait("TestId", "TEST-TASK-DRAFT-04")]
    [Trait("TestId", "TEST-TASK-DRAFT-05")]
    [Trait("TestId", "TEST-TASK-DRAFT-06")]
    [Trait("TestId", "TEST-TASK-DRAFT-07")]
    public async Task SelectedMessages_CreateStructuredReview_AndOnlyConfirmedTasksMutate()
    {
        using var workerFactory = CreateWorkerFactory();
        using var client = workerFactory.CreateClient();
        var data = await SeedAsync(workerFactory.Services);
        var csrf = await GetCsrfTokenAsync(client);

        var accepted = await EnqueueAsync(client, data, csrf, $"native-draft-{Guid.NewGuid():N}");
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(accepted);
        var job = await WaitForJobAsync(client, created.JobId);
        job.Status.Should().Be(AiJobStatuses.Succeeded);
        job.SchemaId.Should().Be(TaskDraftAiContract.SchemaId);
        job.IsMock.Should().BeFalse();
        job.SelectedProvider.Should().Be("IntegrationProvider");
        job.Sources.Should().Contain(source => source.SourceType == "message" && source.SourceEntityId == data.MessageId && source.SourceHash != null);

        await EnsureDraftRowVersionAsync(workerFactory.Services, job.DraftIds.Single());
        var detail = await GetResultAsync<AiDraftDetailDto>(client, $"/api/ai/drafts/{job.DraftIds.Single()}");
        detail.SchemaId.Should().Be(TaskDraftAiContract.SchemaId);
        var payload = detail.WorkingPayload.Deserialize<AiTaskDraftPayload>(JsonOptions)!;
        payload.Tasks.Should().HaveCount(2);
        payload.Tasks.Should().OnlyContain(item => item.SourceRefs!.Single() == $"message:{data.MessageId:D}");

        var reviewed = payload with
        {
            Tasks =
            [
                payload.Tasks[0] with { Title = "Reviewed selected source task", Selected = true },
                payload.Tasks[1] with { Selected = false }
            ]
        };
        var patch = await SendWithCsrfAsync(client, HttpMethod.Patch, $"/api/ai/drafts/{detail.DraftId}",
            new PatchAiDraftDto(JsonSerializer.Serialize(reviewed, JsonOptions), detail.RowVersion), csrf);
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        detail = await ReadResultAsync<AiDraftDetailDto>(patch);

        const string confirmKey = "native-task-draft-confirm";
        var confirm = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/ai/drafts/{detail.DraftId}/confirm",
            new ConfirmAiDraftDto(
                JsonSerializer.Serialize(reviewed, JsonOptions),
                TaskDraftAiContract.ConfirmAction,
                "Reviewed by project manager",
                detail.RowVersion,
                confirmKey),
            csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = confirmKey });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipt = await ReadResultAsync<AiDraftConfirmResultDto>(confirm);
        receipt.CreatedTaskCount.Should().Be(1);

        var replay = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/ai/drafts/{detail.DraftId}/confirm",
            new ConfirmAiDraftDto(null, TaskDraftAiContract.ConfirmAction, null, detail.RowVersion, confirmKey),
            csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = confirmKey });
        (await ReadResultAsync<AiDraftConfirmResultDto>(replay)).CreatedTaskIds.Should().Equal(receipt.CreatedTaskIds);

        var readBack = await GetResultAsync<AiDraftDetailDto>(client, $"/api/ai/drafts/{detail.DraftId}");
        readBack.Status.Should().Be(AiDraftStatuses.Confirmed);
        readBack.ConfirmationResult.Should().NotBeNull();

        using var scope = workerFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var task = await db.TaskItems.SingleAsync(item => receipt.CreatedTaskIds.Contains(item.Id));
        task.Title.Should().Be("Reviewed selected source task");
        (await db.TaskItems.CountAsync(item => item.ProjectId == data.ProjectId)).Should().Be(1);
    }

    [Fact]
    [Trait("TestId", "TEST-TASK-DRAFT-08")]
    [Trait("TestId", "TEST-TASK-DRAFT-09")]
    public async Task CrossTenantAndStaleMessage_DenyWithoutCreatingTasks()
    {
        using var workerFactory = CreateWorkerFactory();
        using var manager = workerFactory.CreateClient();
        var data = await SeedAsync(workerFactory.Services);
        var csrf = await GetCsrfTokenAsync(manager);

        using var outsider = workerFactory.CreateClient();
        outsider.DefaultRequestHeaders.Add("X-Test-UserId", data.OutsiderId.ToString());
        var outsiderCsrf = await GetCsrfTokenAsync(outsider);
        (await EnqueueAsync(outsider, data, outsiderCsrf, $"outsider-{Guid.NewGuid():N}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var accepted = await EnqueueAsync(manager, data, csrf, $"stale-{Guid.NewGuid():N}");
        var created = await ReadResultAsync<AiJobCreatedDto>(accepted);
        var job = await WaitForJobAsync(manager, created.JobId);
        await EnsureDraftRowVersionAsync(workerFactory.Services, job.DraftIds.Single());
        var detail = await GetResultAsync<AiDraftDetailDto>(manager, $"/api/ai/drafts/{job.DraftIds.Single()}");

        using (var scope = workerFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var message = await db.GroupMessages.SingleAsync(item => item.Id == data.MessageId);
            message.Content = "Source changed after the AI draft was generated.";
            message.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
            await db.SaveChangesAsync();
        }

        var confirm = await SendWithCsrfAsync(manager, HttpMethod.Post, $"/api/ai/drafts/{detail.DraftId}/confirm",
            new ConfirmAiDraftDto(null, TaskDraftAiContract.ConfirmAction, null, detail.RowVersion, "stale-native-confirm"),
            csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = "stale-native-confirm" });
        confirm.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var failure = await confirm.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions);
        failure!.ErrorCode.Should().Be(AiErrorCodes.SourceStale);

        using var verifyScope = workerFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.TaskItems.CountAsync(item => item.ProjectId == data.ProjectId)).Should().Be(0);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateWorkerFactory()
        => _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiJobsV4:WorkerEnabled"] = "true",
                ["AiJobsV4:PollIntervalMilliseconds"] = "50"
            })));

    private static async Task<SeededData> SeedAsync(IServiceProvider services)
    {
        var ownerId = Guid.Parse("B0000000-0000-0000-0000-000000000000");
        var outsiderId = Guid.NewGuid();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!await db.Users.AnyAsync(item => item.Id == ownerId))
        {
            db.Users.Add(new User
            {
                Id = ownerId,
                FullName = "Task Draft Owner",
                Email = "task-draft-owner@qaly.test",
                PasswordHash = "not-used",
                Role = "User",
                IsActive = true
            });
        }
        db.Users.Add(new User
        {
            Id = outsiderId,
            FullName = "Task Draft Outsider",
            Email = $"{outsiderId:N}@qaly.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        });
        var group = new WorkGroup { Name = "Source-linked group", OwnerId = ownerId, Status = "Active" };
        var project = new Project
        {
            Name = "Source-linked project",
            Code = $"SL-{Guid.NewGuid():N}"[..12],
            OwnerId = ownerId,
            SourceGroupId = group.Id,
            Status = "Active"
        };
        var message = new GroupMessage
        {
            WorkGroupId = group.Id,
            UserId = ownerId,
            Content = "Please create an API review task with acceptance criteria before Friday.",
            MessageType = "Text"
        };
        db.AddRange(group, project, message);
        await db.SaveChangesAsync();
        return new SeededData(project.Id, group.Id, message.Id, outsiderId);
    }

    private static async Task<HttpResponseMessage> EnqueueAsync(
        HttpClient client,
        SeededData data,
        string csrf,
        string idempotencyKey)
    {
        var request = new AiFunctionJobRequest(
            ProjectId: data.ProjectId,
            Sources: [new AiJobSourceInputDto("message", data.MessageId, null, null, null)],
            ProviderHint: "auto",
            Language: "vi");
        return await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/ai/groups/{data.GroupId}/task-drafts",
            request, csrf, new Dictionary<string, string> { ["Idempotency-Key"] = idempotencyKey });
    }

    private static async Task<AiJobDetailDto> WaitForJobAsync(HttpClient client, Guid jobId)
    {
        for (var attempt = 0; attempt < 80; attempt++)
        {
            var detail = await GetResultAsync<AiJobDetailDto>(client, $"/api/ai/jobs/{jobId}");
            if (detail.Status is AiJobStatuses.Succeeded or AiJobStatuses.Failed or AiJobStatuses.Canceled)
            {
                detail.Status.Should().Be(AiJobStatuses.Succeeded, detail.LastErrorMessage);
                return detail;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Native task draft job did not reach a terminal state.");
    }

    private static async Task EnsureDraftRowVersionAsync(IServiceProvider services, Guid draftId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var draft = await db.AiGeneratedDrafts.SingleAsync(item => item.Id == draftId);
        draft.RowVersion = [1, 2, 3, 4];
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(
        HttpClient client,
        HttpMethod method,
        string url,
        T body,
        string csrf,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (headers != null)
        {
            foreach (var header in headers) request.Headers.Add(header.Key, header.Value);
        }
        return await client.SendAsync(request);
    }

    private static async Task<T> GetResultAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadResultAsync<T>(response);
    }

    private static async Task<T> ReadResultAsync<T>(HttpResponseMessage response)
    {
        var envelope = (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions))!;
        envelope.IsSuccess.Should().BeTrue(envelope.Error);
        envelope.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
        => (await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;

    private sealed record SeededData(Guid ProjectId, Guid GroupId, Guid MessageId, Guid OutsiderId);
    private sealed record CsrfResponse(string Token);
    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error, string? ErrorCode, int StatusCode);
}
