using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.DTOs.Task;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

#pragma warning disable CA1707
public class MeetingActionItemsControllerTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public MeetingActionItemsControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetActionItems_WhenNoMapping_ReturnsItemsWithNotLinkedStatus()
    {
        var seed = await SeedMeetingImportAsync();

        var response = await _client.GetAsync($"/api/meetings/{seed.MeetingImportId}/action-items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<MeetingActionItemsResponseDto>>();
        payload!.IsSuccess.Should().BeTrue(payload.Error);
        payload.Data.Should().NotBeNull();
        payload.Data!.Items.Should().HaveCount(2);
        payload.Data.Items.Should().OnlyContain(item => item.MappingStatus == "NotLinked" && item.TaskId == null);
    }

    [Fact]
    public async Task GetActionItems_WhenMappingExists_ReturnsLinkedStatusAndTaskId()
    {
        var seed = await SeedMeetingImportAsync();
        var linkedTaskId = Guid.NewGuid();

        await SeedTaskAndMappingAsync(seed.ProjectId, seed.MeetingImportId, linkedTaskId, itemIndex: 0);

        var response = await _client.GetAsync($"/api/meetings/{seed.MeetingImportId}/action-items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<MeetingActionItemsResponseDto>>();
        payload!.Data!.Items.Should().Contain(item =>
            item.ItemIndex == 0 &&
            item.MappingStatus == "Linked" &&
            item.TaskId == linkedTaskId);
    }

    [Fact]
    public async Task LinkTask_WhenValid_ReturnsSuccessAndCreatesMapping()
    {
        var seed = await SeedMeetingImportAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId, "Manual existing task");

        var response = await PostWithCsrfAsync(
            $"/api/meetings/{seed.MeetingImportId}/action-items/0/link-task",
            new LinkMeetingActionItemTaskRequest(taskId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<MeetingActionItemTaskLinkDto>>();
        payload!.IsSuccess.Should().BeTrue(payload.Error);
        payload.Data!.IsLinked.Should().BeTrue();
        payload.Data.TaskId.Should().Be(taskId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var mapping = await db.MeetingActionItemMappings.SingleAsync(item =>
            item.MeetingImportId == seed.MeetingImportId &&
            item.ActionItemIndex == 0);
        mapping.TaskId.Should().Be(taskId);
        mapping.Status.Should().Be("Linked");
    }

    [Fact]
    public async Task LinkTask_WhenActionItemAlreadyMapped_ReturnsConflict()
    {
        var seed = await SeedMeetingImportAsync();
        var existingTaskId = Guid.NewGuid();
        var newTaskId = await SeedTaskAsync(seed.ProjectId, "Another task");

        await SeedTaskAndMappingAsync(seed.ProjectId, seed.MeetingImportId, existingTaskId, itemIndex: 0);

        var response = await PostWithCsrfAsync(
            $"/api/meetings/{seed.MeetingImportId}/action-items/0/link-task",
            new LinkMeetingActionItemTaskRequest(newTaskId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LinkTask_WhenMeetingImportNotFound_ReturnsNotFound()
    {
        var response = await PostWithCsrfAsync(
            $"/api/meetings/{Guid.NewGuid()}/action-items/0/link-task",
            new LinkMeetingActionItemTaskRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkTask_WhenItemIndexOutOfRange_ReturnsNotFound()
    {
        var seed = await SeedMeetingImportAsync();
        var taskId = await SeedTaskAsync(seed.ProjectId, "Out of range task");

        var response = await PostWithCsrfAsync(
            $"/api/meetings/{seed.MeetingImportId}/action-items/99/link-task",
            new LinkMeetingActionItemTaskRequest(taskId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTaskLink_WhenNoMapping_ReturnsIsLinkedFalse()
    {
        var seed = await SeedMeetingImportAsync();

        var response = await _client.GetAsync($"/api/meetings/{seed.MeetingImportId}/action-items/0/task-link");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<MeetingActionItemTaskLinkDto>>();
        payload!.Data!.IsLinked.Should().BeFalse();
        payload.Data.TaskId.Should().BeNull();
        payload.Data.Status.Should().Be("NotLinked");
    }

    [Fact]
    public async Task GetTaskLink_WhenMapped_ReturnsIsLinkedTrue()
    {
        var seed = await SeedMeetingImportAsync();
        var linkedTaskId = Guid.NewGuid();

        await SeedTaskAndMappingAsync(seed.ProjectId, seed.MeetingImportId, linkedTaskId, itemIndex: 1);

        var response = await _client.GetAsync($"/api/meetings/{seed.MeetingImportId}/action-items/1/task-link");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<MeetingActionItemTaskLinkDto>>();
        payload!.Data!.IsLinked.Should().BeTrue();
        payload.Data.TaskId.Should().Be(linkedTaskId);
        payload.Data.Status.Should().Be("Linked");
    }

    [Fact]
    public async Task GetTaskMeetingSource_WhenMapped_ReturnsMeetingAndActionItemData()
    {
        var seed = await SeedMeetingImportAsync();
        var taskId = Guid.NewGuid();

        await SeedTaskAndMappingAsync(seed.ProjectId, seed.MeetingImportId, taskId, itemIndex: 0);

        var response = await _client.GetAsync($"/api/tasks/{taskId}/meeting-source");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<TaskMeetingSourceDto>>();
        payload!.Data.Should().NotBeNull();
        payload.Data!.MeetingImportId.Should().Be(seed.MeetingImportId);
        payload.Data.ActionItemIndex.Should().Be(0);
        payload.Data.ActionItemDescription.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateTaskFromActionItem_CreatesTaskAndMappingTrace()
    {
        var seed = await SeedMeetingImportAsync();

        var response = await PostWithCsrfAsync(
            $"/api/meetings/{seed.MeetingImportId}/action-items/0/create-task",
            new MeetingActionItemCreateRequest(null, null, null, null, null, null));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<TaskItemDto>>();
        payload!.IsSuccess.Should().BeTrue(payload.Error);
        payload.Data.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var mapping = await db.MeetingActionItemMappings.SingleAsync(item =>
            item.MeetingImportId == seed.MeetingImportId &&
            item.ActionItemIndex == 0);
        mapping.TaskId.Should().Be(payload.Data!.Id);
        mapping.Status.Should().Be("Linked");
    }

    [Fact]
    public async Task LinkTask_WithoutCsrfToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/meetings/{Guid.NewGuid()}/action-items/0/link-task",
            new LinkMeetingActionItemTaskRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpResponseMessage> PostWithCsrfAsync<TRequest>(string requestUri, TRequest payload)
    {
        var csrf = await GetCsrfTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await _client.SendAsync(request);
    }

    private async Task<string> GetCsrfTokenAsync()
    {
        var payload = await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions);
        return payload!.Token;
    }

    private async Task<(Guid ProjectId, Guid MeetingImportId)> SeedMeetingImportAsync()
    {
        var projectId = Guid.NewGuid();
        var meetingImportId = Guid.NewGuid();
        var aiJobId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var ownerId = _factory.TestUserId;
        var payloadJson = JsonSerializer.Serialize(new MeetingExtractionPayload(
            "meetily-import.v1",
            new string('a', 64),
            new MeetingSummaryDto("Sprint review", DateTimeOffset.UtcNow.AddDays(-1), "Summary", ["PM", "Dev"]),
            [
                new MeetingActionDraftDto(
                    "Prepare release checklist",
                    "Compile checklist and dependencies.",
                    "High",
                    DateTimeOffset.UtcNow.AddDays(2),
                    "Release checklist action from meeting notes.",
                    "Dev A"),
                new MeetingActionDraftDto(
                    "Coordinate QA handoff",
                    "Align QA timeline for sprint close.",
                    "Medium",
                    DateTimeOffset.UtcNow.AddDays(3),
                    "QA handoff action from meeting notes.",
                    "QA B")
            ],
            ["release", "qa"],
            []));

        await EnsureUserExists(ownerId, "Test User", "test@qaly.dev");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Meeting Action Item Test",
            Code = $"meeting-{Guid.NewGuid():N}",
            OwnerId = ownerId
        });

        db.AiJobs.Add(new AiJob
        {
            Id = aiJobId,
            JobType = "AI-06_MEETING_EXTRACT",
            ProjectId = projectId,
            SourceType = "meetily_import",
            SourceId = $"source-{Guid.NewGuid():N}",
            ProviderHint = "local",
            Sensitive = true,
            Status = "DraftReady",
            EstimatedCostUsd = 0.002m,
            CacheKey = Guid.NewGuid().ToString("N"),
            RequestedById = ownerId
        });

        db.AiGeneratedDrafts.Add(new AiGeneratedDraft
        {
            Id = draftId,
            AiJobId = aiJobId,
            ProjectId = projectId,
            DraftType = "MeetingActionItems",
            PayloadJson = payloadJson,
            Status = "Pending"
        });

        db.MeetingImports.Add(new MeetingImport
        {
            Id = meetingImportId,
            ProjectId = projectId,
            ImportedById = ownerId,
            SourceProvider = "meetily",
            SourceId = $"meet-{Guid.NewGuid():N}",
            SourceHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            Title = "Sprint review meeting",
            MeetingStartedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Summary = "Planning follow-up actions",
            TranscriptText = "Action item notes",
            ParticipantsJson = "[]",
            RawPayloadJson = "{}",
            AiJobId = aiJobId,
            AiDraftId = draftId
        });

        await db.SaveChangesAsync();

        return (projectId, meetingImportId);
    }

    private async Task<Guid> SeedTaskAsync(Guid projectId, string title)
    {
        var taskId = Guid.NewGuid();
        var reporterId = _factory.TestUserId;
        await EnsureUserExists(reporterId, "Test User", "test@qaly.dev");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        db.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = reporterId,
            Title = title,
            Priority = "Medium",
            Status = "Todo"
        });
        await db.SaveChangesAsync();
        return taskId;
    }

    private async Task SeedTaskAndMappingAsync(Guid projectId, Guid meetingImportId, Guid taskId, int itemIndex)
    {
        var reporterId = _factory.TestUserId;
        await EnsureUserExists(reporterId, "Test User", "test@qaly.dev");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();

        if (!await db.TaskItems.AnyAsync(item => item.Id == taskId))
        {
            db.TaskItems.Add(new TaskItem
            {
                Id = taskId,
                ProjectId = projectId,
                ReporterId = reporterId,
                Title = $"Mapped task {itemIndex}",
                Priority = "Medium",
                Status = "Todo"
            });
        }

        db.MeetingActionItemMappings.Add(new MeetingActionItemMapping
        {
            MeetingImportId = meetingImportId,
            ActionItemIndex = itemIndex,
            TaskId = taskId,
            Status = "Linked",
            SourceTitle = "Source title",
            SourcePriority = "Medium",
            SourceDueDate = DateTimeOffset.UtcNow.AddDays(2),
            SourceQuote = "Source quote",
            CreatedById = reporterId
        });

        await db.SaveChangesAsync();
    }

    private async Task EnsureUserExists(Guid userId, string name, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!await db.Users.AnyAsync(user => user.Id == userId))
        {
            db.Users.Add(new User { Id = userId, FullName = name, Email = email, IsActive = true });
            await db.SaveChangesAsync();
        }
    }

    private sealed record CsrfResponse(string Token);
    private sealed record ApiResult<T>(bool IsSuccess, T? Data, string? Error, int StatusCode);
}
#pragma warning restore CA1707
