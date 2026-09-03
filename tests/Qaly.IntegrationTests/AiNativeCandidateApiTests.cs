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
using Qaly.Application.DTOs.Meeting;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiNativeCandidateApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;

    public AiNativeCandidateApiTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    [Trait("TestId", "TEST-CAND-007-GROUP-SUMMARY")]
    public async Task SelectedGroupMessages_CreateGroundedReadBack_AndOutsiderCannotRead()
    {
        using var workerFactory = CreateWorkerFactory();
        using var client = workerFactory.CreateClient();
        var data = await SeedAsync(workerFactory.Services);
        var csrf = await GetCsrfTokenAsync(client);
        var response = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/ai/groups/{data.GroupId:D}/summaries",
            new GroupSummaryRequestDto(data.ProjectId, [data.MessageId]), csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = $"group-summary-{Guid.NewGuid():N}" });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(response);
        var job = await WaitForJobAsync(client, created.JobId);
        job.SchemaId.Should().Be(GroupSummaryAiContract.SchemaId);
        job.IsMock.Should().BeFalse();

        var result = await GetResultAsync<AiJobResultDto>(client, $"/api/ai/jobs/{job.JobId:D}/result");
        result.Result.GetProperty("messageRange").GetProperty("messageIds")[0].GetGuid().Should().Be(data.MessageId);
        result.Result.GetProperty("summarySourceRefs")[0].GetString().Should().Be($"message:{data.MessageId:D}");

        using var outsider = workerFactory.CreateClient();
        outsider.DefaultRequestHeaders.Add("X-Test-UserId", data.OutsiderId.ToString());
        (await outsider.GetAsync($"/api/ai/jobs/{job.JobId:D}/result")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var projectOnlyMember = workerFactory.CreateClient();
        projectOnlyMember.DefaultRequestHeaders.Add("X-Test-UserId", data.ProjectOnlyMemberId.ToString());
        (await projectOnlyMember.GetAsync($"/api/ai/jobs/{job.JobId:D}/result")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("TestId", "TEST-CAND-009-DASHBOARD-BRIEF")]
    public async Task DashboardBrief_UsesServerTenantSnapshot_ExcludesPrivateTasks_AndDisablesLegacyEndpoint()
    {
        using var workerFactory = CreateWorkerFactory();
        using var client = workerFactory.CreateClient();
        var data = await SeedAsync(workerFactory.Services);
        var csrf = await GetCsrfTokenAsync(client);
        var response = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/ai/dashboard/strategic-brief",
            new DashboardStrategicBriefRequestDto(data.ProjectId), csrf,
            new Dictionary<string, string> { ["Idempotency-Key"] = $"dashboard-brief-{Guid.NewGuid():N}" });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var created = await ReadResultAsync<AiJobCreatedDto>(response);
        var job = await WaitForJobAsync(client, created.JobId);
        job.SchemaId.Should().Be(DashboardStrategicBriefAiContract.SchemaId);

        var result = await GetResultAsync<AiJobResultDto>(client, $"/api/ai/jobs/{job.JobId:D}/result");
        result.Result.GetProperty("coverage").GetProperty("includedTaskCount").GetInt32().Should().Be(1);
        result.Result.GetProperty("coverage").GetProperty("excludedPrivateTaskCount").GetInt32().Should().Be(1);
        result.Result.GetProperty("requestedById").GetGuid().Should().Be(_factory.TestUserId);

        var legacy = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/dashboard/ai-strategy", new { }, csrf);
        legacy.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    [Trait("TestId", "TEST-CAND-010-MEETING-CHECKNOTE")]
    public async Task MeetingChecknote_PersistsGroundedDraft_AndReloadReadsItBack()
    {
        using var client = _factory.CreateClient();
        var data = await SeedAsync(_factory.Services);
        var csrf = await GetCsrfTokenAsync(client);
        const string transcript = "[09:00] Test User: Hoàn thành API trước thứ sáu.";
        var response = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/meetings/{data.MeetingId:D}/auto-checknote",
            new AutoChecknoteRequest(data.ProjectId, "Weekly checknote", transcript, ["Test User"]), csrf);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadResultAsync<AutoChecknoteResponseDto>(response);
        created.AiJobId.Should().NotBeEmpty();
        created.DraftId.Should().NotBeEmpty();
        created.IsMock.Should().BeFalse();
        created.SummaryEvidence.Should().ContainSingle(item => item.SourceStart == 0 && item.SourceEnd == transcript.Length);
        created.ActionItems.Should().ContainSingle(item => item.SourceEvidence == transcript);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var persistedJob = await db.AiJobs.AsNoTracking().SingleAsync(job => job.Id == created.AiJobId);
            persistedJob.JobType.Should().Be("meeting_checknote");
            persistedJob.SchemaId.Should().Be(MeetingChecknoteAiContract.SchemaId);
            persistedJob.Status.Should().Be(AiJobStatuses.Succeeded);
            MeetingChecknoteAiContract.TryValidateModel(persistedJob.ResultJson!, transcript, out _, out _).Should().BeTrue();
        }

        var readBack = await GetResultAsync<AutoChecknoteResponseDto>(client,
            $"/api/meetings/{data.MeetingId:D}/auto-checknote?projectId={data.ProjectId:D}");
        readBack.MeetingImportId.Should().Be(created.MeetingImportId);
        readBack.DraftId.Should().Be(created.DraftId);
        readBack.Provider.Should().Be("IntegrationProvider");

        var legacy = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/ai/meetings/{data.MeetingId:D}/extract-actions", new { projectId = data.ProjectId }, csrf);
        legacy.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    [Trait("TestId", "TEST-CAND-010-MEETING-SOURCE-BOUNDARY")]
    public async Task MeetingChecknote_RequiresRealLinkedSessionAndGroupParticipation()
    {
        var data = await SeedAsync(_factory.Services);
        const string transcript = "[09:00] Test User: Hoàn thành API trước thứ sáu.";

        using var projectOnlyMember = _factory.CreateClient();
        projectOnlyMember.DefaultRequestHeaders.Add("X-Test-UserId", data.ProjectOnlyMemberId.ToString());
        var memberCsrf = await GetCsrfTokenAsync(projectOnlyMember);
        var forbidden = await SendWithCsrfAsync(
            projectOnlyMember,
            HttpMethod.Post,
            $"/api/meetings/{data.MeetingId:D}/auto-checknote",
            new AutoChecknoteRequest(data.ProjectId, "Unauthorized source", transcript, ["Project member"]),
            memberCsrf);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var owner = _factory.CreateClient();
        var ownerCsrf = await GetCsrfTokenAsync(owner);
        var missing = await SendWithCsrfAsync(
            owner,
            HttpMethod.Post,
            $"/api/meetings/{Guid.NewGuid():D}/auto-checknote",
            new AutoChecknoteRequest(data.ProjectId, "Missing source", transcript, ["Test User"]),
            ownerCsrf);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.MeetingImports.CountAsync(item =>
            item.ProjectId == data.ProjectId &&
            (item.Title == "Unauthorized source" || item.Title == "Missing source"))).Should().Be(0);
    }

    [Theory]
    [InlineData("FORCE_PROVIDER_FAILURE", HttpStatusCode.ServiceUnavailable, AiErrorCodes.ProviderUnavailable, true)]
    [InlineData("FORCE_SCHEMA_INVALID", HttpStatusCode.UnprocessableEntity, AiErrorCodes.SchemaInvalid, false)]
    [Trait("TestId", "TEST-CAND-010-MEETING-FAILURE-LIFECYCLE")]
    public async Task MeetingChecknote_FailurePersistsCanonicalTerminalJob(
        string transcript,
        HttpStatusCode expectedStatus,
        string expectedCode,
        bool retryable)
    {
        using var client = _factory.CreateClient();
        var data = await SeedAsync(_factory.Services);
        var csrf = await GetCsrfTokenAsync(client);
        var response = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/meetings/{data.MeetingId:D}/auto-checknote",
            new AutoChecknoteRequest(data.ProjectId, "Failure checknote", transcript, ["Test User"]), csrf);
        response.StatusCode.Should().Be(expectedStatus);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var failedJob = await db.AiJobs.AsNoTracking()
            .Where(job => job.ProjectId == data.ProjectId && job.JobType == "meeting_checknote")
            .OrderByDescending(job => job.CreatedAt)
            .FirstAsync();
        failedJob.Status.Should().Be(AiJobStatuses.Failed);
        failedJob.LastErrorCode.Should().Be(expectedCode);
        failedJob.LastErrorRetryable.Should().Be(retryable);
        failedJob.ResultJson.Should().BeNull();
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
        var projectOnlyMemberId = Guid.NewGuid();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!await db.Users.AnyAsync(user => user.Id == ownerId))
        {
            db.Users.Add(new User { Id = ownerId, FullName = "Test User", Email = "native-owner@qaly.test", PasswordHash = "not-used", Role = "User", IsActive = true });
        }
        db.Users.Add(new User { Id = outsiderId, FullName = "Outsider", Email = $"{outsiderId:N}@qaly.test", PasswordHash = "not-used", Role = "User", IsActive = true });
        db.Users.Add(new User { Id = projectOnlyMemberId, FullName = "Project-only member", Email = $"{projectOnlyMemberId:N}@qaly.test", PasswordHash = "not-used", Role = "User", IsActive = true });
        var group = new WorkGroup { Name = $"Native group {Guid.NewGuid():N}", OwnerId = ownerId, Status = "Active" };
        var project = new Project { Name = "Native project", Code = $"NP-{Guid.NewGuid():N}"[..12], OwnerId = ownerId, SourceGroupId = group.Id, Status = "Active" };
        var projectMember = new ProjectMember { ProjectId = project.Id, UserId = projectOnlyMemberId, Role = "Member" };
        var message = new GroupMessage { WorkGroupId = group.Id, UserId = ownerId, Content = "Ưu tiên hoàn thành API trước thứ sáu.", MessageType = "Text" };
        var visibleTask = new TaskItem { ProjectId = project.Id, ReporterId = ownerId, Title = "Visible task", Status = "Todo", Priority = "High", ContributesToProgress = true, IsPrivate = false };
        var privateTask = new TaskItem { ProjectId = project.Id, ReporterId = ownerId, Title = "Private secret", Status = "Todo", Priority = "High", ContributesToProgress = true, IsPrivate = true };
        var meeting = new GroupMeetingSession { WorkGroupId = group.Id, StartedByUserId = ownerId, Provider = "Test", RoomId = $"room-{Guid.NewGuid():N}", Status = "Active" };
        db.AddRange(group, project, projectMember, message, visibleTask, privateTask, meeting);
        await db.SaveChangesAsync();
        return new SeededData(project.Id, group.Id, message.Id, meeting.Id, outsiderId, projectOnlyMemberId);
    }

    private static async Task<AiJobDetailDto> WaitForJobAsync(HttpClient client, Guid jobId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var detail = await GetResultAsync<AiJobDetailDto>(client, $"/api/ai/jobs/{jobId:D}");
            if (detail.Status is AiJobStatuses.Succeeded or AiJobStatuses.Failed or AiJobStatuses.Canceled)
            {
                detail.Status.Should().Be(AiJobStatuses.Succeeded, detail.LastErrorMessage);
                return detail;
            }
            await Task.Delay(75);
        }
        throw new TimeoutException("AI native candidate job did not reach a terminal state.");
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(HttpClient client, HttpMethod method, string url, T body, string csrf, IReadOnlyDictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (headers != null) foreach (var header in headers) request.Headers.Add(header.Key, header.Value);
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

    private sealed record SeededData(
        Guid ProjectId,
        Guid GroupId,
        Guid MessageId,
        Guid MeetingId,
        Guid OutsiderId,
        Guid ProjectOnlyMemberId);
    private sealed record CsrfResponse(string Token, string HeaderName);
    private sealed record ApiEnvelope<T>(bool IsSuccess, T? Data, string? Error, string? ErrorCode);
}
