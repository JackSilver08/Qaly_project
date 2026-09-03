using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Application.Common.Telemetry;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiAssistantTurnApiTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AiAssistantTurnApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // This class intentionally verifies cross-project capacity. Each test must
        // therefore start from an isolated portfolio; otherwise canonical projects
        // created by an earlier test become real commitments for the next test and
        // make otherwise feasible staffing scenarios fail for the wrong reason.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CompletedDurableTurn_EmitsAiSpecificSliWithoutEntityIdentifiers()
    {
        var measurements = new ConcurrentBag<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == QalyAiTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, TelemetryTags(tags))));
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, TelemetryTags(tags))));
        listener.Start();

        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Bạn có thể giúp tôi những gì?",
            new AiAssistantClientContextDto("/dashboard", "workspace")));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        measurements.Should().Contain(item => item.Name == "qaly.ai.assistant.first_progress.duration");
        measurements.Should().Contain(item => item.Name == "qaly.ai.assistant.first_answer.duration");
        measurements.Should().Contain(item =>
            item.Name == "qaly.ai.assistant.turns" &&
            Equals(item.Tags["qaly.ai.outcome"], "completed"));
        measurements.SelectMany(item => item.Tags.Keys).Should().NotContain(key =>
            key.Contains("user", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("project_id", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("prompt", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("message", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("TestId", "TEST-UA-04")]
    [Trait("TestId", "TEST-UA-08")]
    public async Task TaskCreateIntent_WithAuthorizedProject_ReturnsRegisteredDraftArtifactWithoutMutation()
    {
        var projectId = await SeedOwnedProjectAsync();
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Tạo ba task frontend, backend và QA cho luồng đăng nhập",
            new AiAssistantClientContextDto("/dashboard", "project_tasks", projectId, "project", projectId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.SchemaId.Should().Be(AiAssistantTurnContract.SchemaId);
        turn.Disposition.Should().Be("registered_action");
        turn.Intent.Should().Be(AiAssistantTurnContract.TaskCreateIntent);
        turn.ExecutionPolicy.Should().Be("draft_then_confirm");
        turn.Artifact.Should().NotBeNull();
        turn.Artifact!.ProjectId.Should().Be(projectId);
        turn.Capabilities.Should().Contain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability &&
            item.ConfirmationPolicy == "explicit_selective_confirm");
        turn.SourceDisclosures.Should().Contain(item =>
            item.SourceId == AiAssistantContextContract.ProjectSummarySource &&
            item.Status == "read");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskItems.CountAsync(item => item.ProjectId == projectId)).Should().Be(0);
        (await db.AiJobs.CountAsync(item => item.ProjectId == projectId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-INTENT-TASK-PURPOSE-01")]
    public async Task TaskCreateIntent_MentioningProjectStartupPurpose_DoesNotRouteToProjectLaunch()
    {
        var projectId = await SeedOwnedProjectAsync();
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "giup toi tao task cho giai doan dau, sprint 1, khao sat va tim tai lieu de khoi tao du an",
            new AiAssistantClientContextDto(
                $"/projects/{projectId}", "project", projectId, "project", projectId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Intent.Should().Be(AiAssistantTurnContract.TaskCreateIntent);
        turn.Disposition.Should().Be("registered_action");
        turn.Artifact.Should().NotBeNull();
        turn.Artifact!.ProjectId.Should().Be(projectId);
        turn.ProjectLaunchBrief.Should().BeNull();
        turn.ProjectLaunchPlan.Should().BeNull();
    }

    [Fact]
    [Trait("TestId", "TEST-UA-05")]
    public async Task TaskCreateIntent_WithoutProject_ReturnsStructuredAuthorizedClarification()
    {
        var projectId = await SeedOwnedProjectAsync();
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Lập task cho API thanh toán",
            new AiAssistantClientContextDto("/dashboard", "project_tasks")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Disposition.Should().Be("clarification_required");
        turn.Intent.Should().Be(AiAssistantTurnContract.ClarificationIntent);
        turn.Artifact.Should().BeNull();
        turn.Clarification.Should().NotBeNull();
        turn.Clarification!.Field.Should().Be("projectId");
        turn.Clarification.Choices.Should().Contain(choice => choice.Id == projectId.ToString());
    }

    [Fact]
    [Trait("TestId", "TEST-UA-06")]
    public async Task ProjectCreateRequest_ReturnsLaunchBriefWithoutCreatingProject()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        int jobCountBefore;
        int projectCountBefore;
        using (var beforeScope = _factory.Services.CreateScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            jobCountBefore = await beforeDb.AiJobs.CountAsync();
            projectCountBefore = await beforeDb.Projects.CountAsync();
        }

        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Tạo một dự án mới tên Alpha",
            new AiAssistantClientContextDto("/dashboard", "projects", OrganizationId: organizationId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Disposition.Should().Be("project_launch_brief");
        turn.Intent.Should().Be(AiProjectLaunchContract.CapabilityId);
        turn.ExecutionPolicy.Should().Be("read_only_proposal");
        turn.Artifact.Should().BeNull();
        turn.ProjectLaunchBrief.Should().NotBeNull();
        turn.ProjectLaunchBrief!.OrganizationId.Should().Be(organizationId);
        turn.ProjectLaunchPlan.Should().BeNull();
        turn.GoalAnalysis.Should().NotBeNull();
        turn.GoalAnalysis!.SelectedSkills.Should().Contain(item => item.SkillId == AiProjectLaunchContract.CapabilityId);
        turn.WorkPlan.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AiJobs.CountAsync()).Should().Be(jobCountBefore);
        (await db.Projects.CountAsync()).Should().Be(projectCountBefore);
    }

    [Fact]
    [Trait("TestId", "TEST-PL-E2E-AUTO-01")]
    public async Task CompleteProjectLaunchRequest_AutoBuildsStaffingDeliveryPlan_WithoutPrematureMutation()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Khởi chạy dự án web SPA trong 12 tuần cho khách hàng; must-have đặt dịch vụ, quản lý phòng, thanh toán và dashboard; chỉ định manager, thành viên, sprint, task và phân việc.",
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn!.Disposition.Should().Be("project_launch_plan");
        turn.ProjectLaunchBrief.Should().NotBeNull();
        turn.ProjectLaunchBrief!.Questions.Should().BeEmpty();
        turn.ProjectLaunchPlan.Should().NotBeNull();
        turn.ProjectLaunchPlan!.DeliveryPlan.Sprints.Should().NotBeEmpty();
        turn.ProjectLaunchPlan.StaffingScenarios.Should().NotBeEmpty();
        turn.ProjectLaunchPlan.ExecutionReceipt.Should().BeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"))).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-SESSION-CONTEXT-01")]
    public async Task ShortFollowUp_UsesDurableServerHistory_AndContinuesProjectLaunch()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var session = await CreateSessionAsync();
        var firstResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Mình muốn tự động tạo dự án web SPA",
            new AiAssistantClientContextDto("/analytics", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-context-first-{Guid.NewGuid():N}");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
        var first = (await firstResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        first.Intent.Should().Be(AiProjectLaunchContract.CapabilityId);
        first.ProjectLaunchBrief.Should().NotBeNull();

        var followUpResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "tiếp tục nhé",
            new AiAssistantClientContextDto("/analytics", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: first.SessionVersion,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-context-followup-{Guid.NewGuid():N}");
        followUpResponse.StatusCode.Should().Be(HttpStatusCode.OK, await followUpResponse.Content.ReadAsStringAsync());
        var followUp = (await followUpResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;

        followUp.Intent.Should().Be(AiProjectLaunchContract.CapabilityId);
        followUp.ProjectLaunchBrief.Should().NotBeNull();
        followUp.ProjectLaunchBrief!.Revision.Should().Be(2);
        followUp.AssistantMessage.Should().NotContain("Xin chào! Mình là Erumi");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P04-DURABLE-MEMORY-01")]
    public async Task P04SessionMemory_IsRecalledAfterCanonicalSessionReload()
    {
        const string memory = "Ghi nhớ rằng trong phiên này “MVP” nghĩa là ba chức năng: đăng ký, đặt lịch và thanh toán. Chưa tạo dữ liệu; chỉ xác nhận ngắn.";
        var session = await CreateSessionAsync();
        var firstResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            memory,
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-memory-first-{Guid.NewGuid():N}");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
        var first = (await firstResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        first.Intent.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        first.Answer!.Intent.Should().Be("session_memory_ack");
        first.AssistantMessage.Should().Contain("đăng ký, đặt lịch và thanh toán");
        first.Artifact.Should().BeNull();

        var restored = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{session.SessionId:D}", JsonOptions);
        restored!.Turns.Should().ContainSingle(turn => turn.UserMessage == memory);

        var recallResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Trong phiên này MVP nghĩa là gì?",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            SessionId: restored.SessionId,
            ExpectedVersion: restored.Version,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-memory-recall-{Guid.NewGuid():N}");
        recallResponse.StatusCode.Should().Be(HttpStatusCode.OK, await recallResponse.Content.ReadAsStringAsync());
        var recalled = (await recallResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        recalled.Intent.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        recalled.Answer!.Intent.Should().Be("session_memory_recall");
        recalled.AssistantMessage.Should().Contain("đăng ký, đặt lịch và thanh toán");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-NAV-01")]
    public async Task CapabilityOverview_ReturnsDurableStructuredNavigationWithoutProviderDependency()
    {
        _ = await SeedOwnedOrganizationWithRulebookAsync();
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Bạn có thể giúp cho tôi những gì?",
            new AiAssistantClientContextDto("/dashboard", "workspace")));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Disposition.Should().Be("grounded_answer");
        turn.Intent.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        turn.Answer.Should().NotBeNull();
        turn.Answer!.Intent.Should().Be("capability_overview");
        turn.Answer.UsedAi.Should().BeFalse();
        turn.Answer.Tables.Should().ContainSingle(table => table.Title == "Quyền AI trong ngữ cảnh hiện tại");
        turn.Answer.Tables.Single().Rows.Should().Contain(row =>
            row.Values.Any(value => value != null &&
                string.Equals(value.ToString(), "EXTERNAL_DEFERRED", StringComparison.Ordinal)));
        turn.Answer.Actions.Should().HaveCount(5);
        turn.Answer.Actions.Should().OnlyContain(action => action.Type == "assistant_navigation");
        var payloads = turn.Answer.Actions
            .Select(action => JsonSerializer.Serialize(action.Payload, JsonOptions))
            .ToArray();
        payloads.Should().Contain(payload => payload.Contains("/projects", StringComparison.Ordinal));
        payloads.Should().Contain(payload => payload.Contains("/analytics", StringComparison.Ordinal));

        var launchResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Bạn có thể giúp tôi khởi tạo 1 dự án về web cung cấp dịch vụ spa theo gói được không?",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            SessionId: turn.SessionId,
            ExpectedVersion: turn.SessionVersion,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-navigation-launch-{Guid.NewGuid():N}");
        launchResponse.StatusCode.Should().Be(HttpStatusCode.OK, await launchResponse.Content.ReadAsStringAsync());
        var launchTurn = await launchResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        launchTurn.Should().NotBeNull();
        launchTurn!.Intent.Should().Be(AiProjectLaunchContract.CapabilityId);
        launchTurn.Answer?.Intent.Should().NotBe("capability_overview");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P28-EXTERNAL-DEFERRED-01")]
    public async Task P28ExternalAdapters_ReturnsFiveVerifiedDeferredRowsWithoutMutationOrProviderDependency()
    {
        var projectId = await SeedOwnedProjectAsync();
        int projectsBefore;
        int tasksBefore;
        using (var beforeScope = _factory.Services.CreateScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            projectsBefore = await beforeDb.Projects.CountAsync();
            tasksBefore = await beforeDb.TaskItems.CountAsync();
        }

        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Kiểm tra khả năng đồng bộ calendar, repository, invitation, webhook và deployment cho Project này. " +
            "Chỉ đánh dấu hoàn thành nếu adapter thật đã đọc/ghi và read-back; phần chưa có phải ghi EXTERNAL_DEFERRED.",
            new AiAssistantClientContextDto($"/projects/{projectId:D}", "project", projectId, "project", projectId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = (await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        turn.Intent.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        turn.ExecutionPolicy.Should().Be("read_only");
        turn.Answer.Should().NotBeNull();
        turn.Answer!.Intent.Should().Be("external_adapter_status");
        turn.Answer.UsedAi.Should().BeFalse();
        turn.Answer.Model!.Provider.Should().Be("Qaly");
        turn.Answer.Tables.Should().ContainSingle(table => table.Title == "Trạng thái adapter bên ngoài");
        var rows = turn.Answer.Tables.Single().Rows;
        rows.Should().HaveCount(5);
        rows.Select(row => row["adapter"]?.ToString()).Should().BeEquivalentTo(
            ["Calendar", "Repository", "Invitation", "Webhook", "Deployment"]);
        rows.Should().OnlyContain(row => row["status"]!.ToString() == "EXTERNAL_DEFERRED");
        turn.Answer.Actions.Should().OnlyContain(action => action.Type == "assistant_navigation");

        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await afterDb.Projects.CountAsync()).Should().Be(projectsBefore);
        (await afterDb.TaskItems.CountAsync()).Should().Be(tasksBefore);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P01-MEMBER-CAPABILITIES-01")]
    public async Task P01CapabilityOverview_ForMemberShowsReadOnlyCardsWithoutMutationCapability()
    {
        var projectId = await SeedForeignProjectAsync(addCurrentUserAsViewer: true);
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Dựa trên role và ngữ cảnh hiện tại, hãy cho tôi biết bạn làm được gì. Trả bằng các card hành động ngắn, chia rõ: chỉ xem, tạo bản nháp, cần xác nhận và chưa được hỗ trợ. Mỗi card có nút mở đúng màn hình.",
            new AiAssistantClientContextDto("/projects", "project", projectId, "project", projectId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = (await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        turn.Intent.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        turn.ExecutionPolicy.Should().Be("read_only");
        turn.Capabilities.Should().Contain(item => item.CapabilityId == AiAssistantContextContract.GroundedReadCapability);
        turn.Capabilities.Should().NotContain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability ||
            item.CapabilityId == AiAssistantContextContract.TaskAssignmentScheduleCapability);
        turn.Answer!.Tables.Should().ContainSingle(table => table.Title == "Quyền AI trong ngữ cảnh hiện tại");
        var values = turn.Answer.Tables.Single().Rows.SelectMany(row => row.Values).Select(value => value?.ToString()).ToArray();
        values.Should().Contain("Chỉ xem");
        values.Should().Contain("EXTERNAL_DEFERRED");
        turn.Answer.Actions.Should().OnlyContain(action => action.Type == "assistant_navigation");
        var taskActionPayload = (JsonElement)turn.Answer.Actions.Single(action => action.Label == "Nhiệm vụ").Payload!;
        taskActionPayload.GetProperty("description").GetString()
            .Should().Contain("không hiển thị thao tác tạo bằng AI");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P25-MEMBER-READ-ONLY-01")]
    public async Task P25MemberReadOnly_ReturnsUsefulCanonicalSummaryAndSourceNavigationWithoutMutationControl()
    {
        var projectId = await SeedForeignProjectAsync(addCurrentUserAsViewer: true);
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Tóm tắt Project và tài liệu tôi được phép xem, sau đó đề xuất ba việc nên làm. Nếu tôi không có quyền tạo/giao Task, vẫn trả phân tích hữu ích và nút mở dữ liệu nguồn; không hiện nút xác nhận mutation.",
            new AiAssistantClientContextDto($"/projects/{projectId:D}", "project", projectId, "project", projectId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = (await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        turn.ExecutionPolicy.Should().Be("read_only");
        turn.Answer!.Intent.Should().Be("member_read_only_project_summary");
        turn.Answer.Tables.Should().ContainSingle(table =>
            table.Title == "Ba việc nên làm" && table.Rows.Count == 3);
        turn.Answer.Actions.Should().HaveCount(3);
        turn.Answer.Actions.Should().OnlyContain(action =>
            action.Type == "assistant_navigation" && !action.RequiresConfirmation);
        turn.Answer.Actions.Should().Contain(action => action.Label == "Mở tổng quan dự án");
        turn.Answer.Actions.Should().Contain(action => action.Label == "Mở Task nguồn");
        turn.Answer.Actions.Should().Contain(action => action.Label == "Mở Wiki dự án");
        turn.Capabilities.Should().NotContain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability ||
            item.CapabilityId == AiAssistantContextContract.TaskAssignmentScheduleCapability);
        turn.Artifact.Should().BeNull();
        turn.NativeActionDraft.Should().BeNull();
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P27-RENDERER-NAVIGATION-01")]
    public async Task P27RendererNavigation_ReturnsBoundedTypedTablesWithCanonicalRowLinks()
    {
        var projectId = await SeedRendererNavigationProjectAsync();
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Liệt kê ba Task quá hạn, workload ba người cao nhất và Sprint có nguy cơ. Dùng metric/table/task cards phù hợp; mỗi item có nút mở đúng đối tượng, không trả một khối text dài.",
            new AiAssistantClientContextDto($"/projects/{projectId:D}", "project", projectId, "project", projectId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = (await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        turn.ExecutionPolicy.Should().Be("read_only");
        turn.Answer!.Intent.Should().Be("project_renderer_navigation");
        turn.Answer.Metrics.Should().HaveCount(3);
        turn.Answer.Tables.Should().HaveCount(3);

        var taskTable = turn.Answer.Tables.Single(table => table.Title == "Ba Task quá hạn");
        taskTable.Rows.Should().HaveCount(3);
        taskTable.RowAction.Should().Be(new ErumiTableRowActionDto("Mở Task"));
        taskTable.Rows.Should().OnlyContain(row =>
            row["route"]!.ToString()!.StartsWith($"/projects/{projectId:D}/tasks/", StringComparison.Ordinal));

        var memberTable = turn.Answer.Tables.Single(table => table.Title == "Ba thành viên có tải cao nhất");
        memberTable.Rows.Should().HaveCount(3);
        memberTable.RowAction.Should().Be(new ErumiTableRowActionDto("Mở thành viên"));
        memberTable.Rows.Should().OnlyContain(row =>
            row["route"] != null &&
            string.Equals(row["route"]!.ToString(), $"/projects/{projectId:D}?tab=members", StringComparison.Ordinal));

        var sprintTable = turn.Answer.Tables.Single(table => table.Title == "Sprint có nguy cơ");
        sprintTable.Rows.Should().ContainSingle();
        sprintTable.RowAction.Should().Be(new ErumiTableRowActionDto("Mở Sprint"));
        sprintTable.Rows.Single()["route"]!.ToString().Should().StartWith($"/projects/{projectId:D}#milestone-");
        turn.Answer.Reply.Length.Should().BeLessThan(320);
        turn.Artifact.Should().BeNull();
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P02-WORKSPACE-01")]
    public async Task P02WorkspaceSummary_StaysReadOnlyAndReturnsCanonicalStructuredData()
    {
        var projectId = await SeedOwnedProjectAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.TaskItems.Add(new TaskItem
            {
                ProjectId = projectId,
                ReporterId = _factory.TestUserId,
                Title = "P02 overdue fixture",
                Status = "Todo",
                DueDate = DateTimeOffset.UtcNow.AddDays(-2)
            });
            await db.SaveChangesAsync();
        }

        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Tóm tắt workspace hiện tại: số Project đang hoạt động, tiến độ, task quá hạn, workload cao và ba việc cần chú ý. Dùng metric/table/card phù hợp, có link nguồn; phần quy trình collapse mặc định.",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            Mode: "agent"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = (await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        turn.Intent.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        turn.ExecutionPolicy.Should().Be("read_only");
        turn.Artifact.Should().BeNull();
        turn.GoalAnalysis!.SelectedSkills.Should().ContainSingle(item =>
            item.SkillId == AiAssistantContextContract.GroundedReadCapability);
        turn.Answer.Should().NotBeNull();
        turn.Answer!.Intent.Should().Be("workspace_risk");
        turn.Answer.Metrics.Should().NotBeEmpty();
        turn.Answer.Tables.Should().NotBeEmpty();
        turn.Answer.Sources.Should().NotBeEmpty();
        turn.AssistantMessage.Should().NotContain("chỉ hỗ trợ soạn bản nháp để tạo task mới");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P03-PROJECT-SCOPE-01")]
    public async Task P03SelectedProjectOnGroupRoute_UsesSelectedProjectSources()
    {
        var projectId = await SeedOwnedProjectAsync();
        Guid groupId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var group = new WorkGroup { Name = "P03 ambient group", OwnerId = _factory.TestUserId };
            db.WorkGroups.Add(group);
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = _factory.TestUserId,
                Role = "Manager"
            });
            db.TaskItems.AddRange(
                new TaskItem
                {
                    ProjectId = projectId,
                    ReporterId = _factory.TestUserId,
                    AssigneeId = _factory.TestUserId,
                    Title = "P03 completed fixture",
                    Status = "Done",
                    EstimatedHours = 4,
                    DueDate = DateTimeOffset.UtcNow.AddDays(-3),
                    UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new TaskItem
                {
                    ProjectId = projectId,
                    ReporterId = _factory.TestUserId,
                    AssigneeId = _factory.TestUserId,
                    Title = "P03 in-progress fixture",
                    Status = "InProgress",
                    EstimatedHours = 8,
                    DueDate = DateTimeOffset.UtcNow.AddDays(2)
                },
                new TaskItem
                {
                    ProjectId = projectId,
                    ReporterId = _factory.TestUserId,
                    AssigneeId = _factory.TestUserId,
                    Title = "P03 overdue fixture",
                    Status = "Todo",
                    EstimatedHours = 3,
                    DueDate = DateTimeOffset.UtcNow.AddDays(-1)
                });
            await db.SaveChangesAsync();
            groupId = group.Id;
        }

        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Phân tích Project đang chọn: mục tiêu, tiến độ Sprint, task nghẽn, dependency, workload, rủi ro deadline và ba hành động ưu tiên. Chỉ dùng dữ liệu tôi được phép xem.",
            new AiAssistantClientContextDto(
                $"/groups/{groupId}",
                "groups",
                projectId,
                "group",
                groupId),
            Mode: "agent"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = (await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        turn.Intent.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        turn.SourceDisclosures.Should().Contain(item =>
            item.SourceId == AiAssistantContextContract.ProjectSummarySource && item.Status == "read");
        turn.SourceDisclosures.Should().Contain(item =>
            item.SourceId == AiAssistantContextContract.ProjectTasksSource && item.Status == "read");
        turn.SourceDisclosures.Should().NotContain(item =>
            item.SourceId == AiAssistantContextContract.GroupContextSource && item.Status == "read");
        turn.AssistantMessage.Should().NotContain("chưa có dữ liệu Project cụ thể");
        turn.AssistantMessage.Should().Contain("### Kết luận");
        turn.AssistantMessage.Should().Contain("### Ba việc ưu tiên");
        turn.Answer.Should().NotBeNull();
        turn.Answer!.Intent.Should().Be("project_analysis");
        turn.Answer.Metrics.Should().NotBeEmpty();
        turn.Answer.Charts.Should().Contain(item => item.Title == "Task đang mở theo thành viên");
        turn.Answer.Charts.Should().NotContain(item => item.Title == "Task theo trạng thái");
        foreach (var chart in turn.Answer.Charts)
        {
            chart.Type.Should().BeOneOf("bar", "pie", "line");
            chart.Labels.Should().NotBeEmpty();
            chart.Labels.Count.Should().Be(chart.Values.Count);
            chart.Values.Should().OnlyContain(value => double.IsFinite(value));
            chart.Values.Should().Contain(value => value > 0);
        }
    }

    [Fact]
    [Trait("TestId", "TEST-PL-A-01")]
    [Trait("TestId", "TEST-RO-LOOP-01")]
    public async Task ProjectLaunch_CreatesDurableRulebookBriefThroughBoundedReadOnlyLoop_WithoutProjectMutation()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        int projectsBefore;
        using (var beforeScope = _factory.Services.CreateScope())
        {
            projectsBefore = await beforeScope.ServiceProvider.GetRequiredService<QalyDbContext>().Projects.CountAsync();
        }

        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Khởi chạy một dự án mới làm web SPA production-ready",
            new AiAssistantClientContextDto(
                "/dashboard", "workspace", OrganizationId: organizationId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Disposition.Should().Be("project_launch_brief");
        turn.Intent.Should().Be(AiProjectLaunchContract.CapabilityId);
        turn.ExecutionPolicy.Should().Be("read_only_proposal");
        turn.Artifact.Should().BeNull();
        turn.ProjectLaunchBrief.Should().NotBeNull();
        turn.ProjectLaunchBrief!.RulebookStatus.Should().Be("effective");
        turn.ProjectLaunchBrief.RuleDecisions.Should().Contain(item =>
            item.RuleKey == "active_membership_required" && item.Result == "pass");
        turn.Conversation.Should().NotBeNull();
        turn.Conversation!.Questions.Should().HaveCountLessThanOrEqualTo(3);
        turn.Conversation.Guidance.Should().BeNull("registered Project Launch is available and must not show a manual fallback");
        turn.WorkPlan!.Steps.Select(item => item.Kind).Should().Equal("retrieve", "analyze", "verify", "present");
        turn.WorkPlan.Steps.Should().OnlyContain(item => item.MutationClass == "none" && item.State == "completed");
        turn.ProcessEvents.Should().Contain(item => item.Stage == "retrieve" && item.Status == "verified");
        turn.ProcessEvents.Should().Contain(item => item.Stage == "verify" && item.Status == "verified");
        turn.ProcessEvents.Should().Contain(item =>
            item.Stage == "analyze" && item.ActualProvider == "DeepSeek" && item.ActualModel == "deepseek-v4-pro");

        var followUp = new AiAssistantTurnRequestDto(
            "8 tuần",
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: turn.SessionId,
            ExpectedVersion: turn.SessionVersion,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId,
            ProgressiveReply: new AiAssistantProgressiveReplyDto("launch.deadline", "8_weeks", "8 tuần"));
        var followUpResponse = await SendTurnAsync(followUp, $"assistant-launch-followup-{Guid.NewGuid():N}");
        followUpResponse.StatusCode.Should().Be(HttpStatusCode.OK, await followUpResponse.Content.ReadAsStringAsync());
        var revised = await followUpResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        revised!.ProjectLaunchBrief!.Revision.Should().Be(2);
        revised.ProjectLaunchBrief.Objective.Should().Be(turn.ProjectLaunchBrief.Objective);
        revised.Conversation!.Questions.Should().NotContain(item => item.Id == "launch.deadline");
        revised.Conversation.Questions.Should().HaveCountLessThanOrEqualTo(3);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.Projects.CountAsync()).Should().Be(projectsBefore);
        (await db.ProjectLaunchBriefs.CountAsync(item => item.OrganizationId == organizationId)).Should().Be(2);
        (await db.OrganizationWorkRuleDecisions.CountAsync()).Should().BeGreaterThan(0);
        (await db.AssistantArtifactRefs.AnyAsync(item =>
            item.TurnId == turn.TurnId && item.RendererId == AiProjectLaunchContract.RendererId)).Should().BeTrue();
    }

    [Fact]
    [Trait("TestId", "TEST-PL-A-DEGRADED-01")]
    public async Task ProjectLaunch_WhenProviderFails_ReturnsLocalRulesBriefInsteadOfFailedTurn()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Khởi tạo dự án web SPA SIMULATE_LAUNCH_PROVIDER_FAILURE",
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn!.Disposition.Should().Be("project_launch_brief");
        turn.TurnStatus.Should().Be("completed");
        turn.ProjectLaunchBrief!.ActualProvider.Should().Be("LocalRules");
        turn.ProjectLaunchBrief.ActualModel.Should().Be("project-launch-fallback-v1");
        turn.Conversation!.ActionDisposition.Should().Be("available");
        turn.Conversation.CapabilityGap.Should().BeNull();
    }

    [Fact]
    [Trait("TestId", "TEST-PL-BCD-01")]
    public async Task ProjectLaunchPlan_ConfirmMonitorRollback_IsAtomicIdempotentAndReviewBounded()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        plan.State.Should().Be("pending_review");
        plan.StaffingScenarios.Should().Contain(item => item.Feasible);
        var verifiedScenario = plan.StaffingScenarios.First(item => item.Feasible);
        verifiedScenario.Members.Should().OnlyContain(item => item.WeeklyAllocation != null && item.WeeklyAllocation.Count > 0);
        verifiedScenario.Members.Sum(item => item.ReviewerCoordinationHours).Should().BeGreaterThan(0m);
        verifiedScenario.RuleDecisions.Should().Contain(item =>
            item.RuleKey == "time_phased_weekly_capacity" && item.Result == "pass");
        verifiedScenario.RuleDecisions.Should().Contain(item =>
            item.RuleKey == "reviewer_coordination_overhead_percent" && item.Result == "pass");
        plan.DeliveryPlan.Sprints.SelectMany(item => item.Tasks).Should().HaveCount(2);
        plan.ExecutionReceipt.Should().BeNull();

        using (var beforeScope = _factory.Services.CreateScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            (await beforeDb.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"))).Should().Be(0);
        }

        var scenarioId = plan.StaffingScenarios.First(item => item.Feasible).ScenarioId;
        var confirmRequest = new ConfirmProjectLaunchPlanRequestDto(true, plan.RowRevision, scenarioId);
        var idempotencyKey = $"integration-launch-{plan.PlanId:N}";
        var confirmed = await SendLaunchCommandAsync<ConfirmProjectLaunchPlanRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm", confirmRequest, idempotencyKey);
        var replayed = await SendLaunchCommandAsync<ConfirmProjectLaunchPlanRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm", confirmRequest, idempotencyKey);

        confirmed.State.Should().Be("executed");
        confirmed.ExecutionReceipt.Should().NotBeNull();
        confirmed.ExecutionReceipt!.InternalTransactionCommitted.Should().BeTrue();
        confirmed.ExecutionReceipt.ReadBackVerified.Should().BeTrue();
        replayed.ExecutionReceipt!.ReceiptId.Should().Be(confirmed.ExecutionReceipt.ReceiptId);
        confirmed.ExecutionReceipt.Commands.Should().Contain(item => item.AdapterId == "project.create.v1" && item.Status == "applied");
        confirmed.ExecutionReceipt.Commands.Should().Contain(item => item.Status == "external_deferred");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            (await db.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"))).Should().Be(1);
            (await db.ProjectMembers.CountAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId)).Should().Be(1);
            (await db.Set<Sprint>().CountAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId)).Should().Be(1);
            (await db.TaskItems.CountAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId)).Should().Be(2);
            var launchTraces = await db.ProjectLaunchTaskTraces
                .Where(item => item.TaskItem.ProjectId == confirmed.ExecutionReceipt.ProjectId)
                .ToListAsync();
            launchTraces.Should().HaveCount(2);
            launchTraces.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.FeatureId) &&
                !string.IsNullOrWhiteSpace(item.SprintClientId) && !string.IsNullOrWhiteSpace(item.TaskClientId) &&
                item.ObjectiveMetricIdsJson != "[]");
            (await db.TaskDependencies.CountAsync(item => item.Predecessor.ProjectId == confirmed.ExecutionReceipt.ProjectId)).Should().Be(1);
            var overdueTask = await db.TaskItems.FirstAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId);
            overdueTask.DueDate = DateTimeOffset.UtcNow.AddDays(-1);
            overdueTask.AssigneeId = null;
            var projectMember = await db.ProjectMembers.SingleAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId);
            projectMember.Role = ProjectRoleRules.Member;
            var sprint = await db.Set<Sprint>().SingleAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId);
            sprint.Goal = "Mục tiêu đã bị đổi sau khi duyệt";
            var dependency = await db.TaskDependencies.SingleAsync(item => item.Predecessor.ProjectId == confirmed.ExecutionReceipt.ProjectId);
            db.TaskDependencies.Remove(dependency);
            launchTraces[0].FeatureId = "feature-drifted-after-confirm";
            await db.SaveChangesAsync();
        }

        var monitored = await SendLaunchCommandAsync<MonitorProjectLaunchExecutionRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/executions/{confirmed.ExecutionReceipt.ReceiptId:D}/monitor",
            new MonitorProjectLaunchExecutionRequestDto(confirmed.ExecutionReceipt.Revision));
        monitored.LatestReplanProposal.Should().NotBeNull();
        monitored.LatestReplanProposal!.State.Should().Be("pending_review");
        monitored.LatestReplanProposal.Changes.Should().Contain(item => item.ChangeType == "tasks_overdue");
        monitored.LatestReplanProposal.Changes.Should().Contain(item => item.ChangeType == "task_assignment_changed");
        monitored.LatestReplanProposal.Changes.Should().Contain(item => item.ChangeType == "task_dependency_changed");
        monitored.LatestReplanProposal.Changes.Should().Contain(item => item.ChangeType == "task_traceability_changed");
        monitored.LatestReplanProposal.Changes.Should().Contain(item => item.ChangeType == "staffing_or_role_changed");
        monitored.LatestReplanProposal.Changes.Should().Contain(item => item.ChangeType == "sprint_baseline_changed");
        monitored.LatestReplanProposal.RequiresConfirmation.Should().BeTrue();

        using (var monitorScope = _factory.Services.CreateScope())
        {
            var db = monitorScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var task = await db.TaskItems.FirstAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId);
            task.DueDate.Should().BeBefore(DateTimeOffset.UtcNow);
            task.Status.Should().Be("Todo");
        }

        const string p24 = "So sánh Project hiện tại với baseline đã xác nhận, chỉ ra drift về scope, lịch, staffing và task; đề xuất replan có before/after nhưng chưa mutation.";
        var assistantMonitorResponse = await PostTurnAsync(new AiAssistantTurnRequestDto(
            p24,
            new AiAssistantClientContextDto(
                $"/projects/{confirmed.ExecutionReceipt.ProjectId:D}",
                "project",
                confirmed.ExecutionReceipt.ProjectId,
                "project",
                confirmed.ExecutionReceipt.ProjectId,
                OrganizationId: organizationId)));
        assistantMonitorResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await assistantMonitorResponse.Content.ReadAsStringAsync());
        var assistantMonitor = (await assistantMonitorResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        assistantMonitor.Intent.Should().Be(AiProjectOrchestrationContract.MonitorCapabilityId);
        assistantMonitor.Disposition.Should().Be("project_replan_proposal");
        assistantMonitor.ProjectLaunchPlan!.LatestReplanProposal!.Changes.Should().NotBeEmpty();
        assistantMonitor.ProjectLaunchPlan.LatestReplanProposal.Changes.Should().OnlyContain(change =>
            change.BaselineValue != change.CurrentValue &&
            !string.IsNullOrWhiteSpace(change.Summary) &&
            !string.IsNullOrWhiteSpace(change.SuggestedAction));
        assistantMonitor.ProjectLaunchPlan.LatestReplanProposal.RequiresConfirmation.Should().BeTrue();
        assistantMonitor.AssistantMessage.Should().Contain("No Project data was changed automatically");

        var rollbackResponse = await SendLaunchCommandResponseAsync(
            $"/api/ai/project-launch/executions/{confirmed.ExecutionReceipt.ReceiptId:D}/rollback",
            new RollbackProjectLaunchExecutionRequestDto(true, monitored.ExecutionReceipt!.Revision, "Integration rollback before user work"),
            $"integration-rollback-{confirmed.ExecutionReceipt.ReceiptId:N}");
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "changing a Task after launch is user work and must never be erased by rollback");
        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var retainedProject = await finalDb.Projects.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == confirmed.ExecutionReceipt.ProjectId);
        retainedProject.IsDeleted.Should().BeFalse();
        (await finalDb.AuditLogs.CountAsync(item => item.EntityId == plan.PlanId.ToString() || item.EntityId == confirmed.ExecutionReceipt.ReceiptId.ToString()))
            .Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-P10-IDEMPOTENCY-READBACK-01")]
    public async Task P10IdempotencyReadBack_ReturnsSameReceiptAndDoesNotDuplicateCanonicalGraph()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenarioId = plan.StaffingScenarios.First(item => item.Feasible).ScenarioId;
        var request = new ConfirmProjectLaunchPlanRequestDto(true, plan.RowRevision, scenarioId);
        var idempotencyKey = $"p10-project-launch-{plan.PlanId:N}";
        var confirmed = await SendLaunchCommandAsync<ConfirmProjectLaunchPlanRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm", request, idempotencyKey);
        var replayed = await SendLaunchCommandAsync<ConfirmProjectLaunchPlanRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm", request, idempotencyKey);

        confirmed.ExecutionReceipt.Should().NotBeNull();
        replayed.ExecutionReceipt!.ReceiptId.Should().Be(confirmed.ExecutionReceipt!.ReceiptId);
        var projectId = confirmed.ExecutionReceipt.ProjectId;
        int projectCount;
        int sprintCount;
        int taskCount;
        int dependencyCount;
        using (var beforeScope = _factory.Services.CreateScope())
        {
            var db = beforeScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            projectCount = await db.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"));
            sprintCount = await db.Set<Sprint>().CountAsync(item => item.ProjectId == projectId);
            taskCount = await db.TaskItems.CountAsync(item => item.ProjectId == projectId);
            dependencyCount = await db.TaskDependencies.CountAsync(item => item.Predecessor.ProjectId == projectId);
        }

        const string p10 = "Mở lại kết quả thực thi Project vừa rồi và kiểm tra xem retry cùng yêu cầu có tạo trùng Project, Sprint hoặc Task không. Chỉ báo theo dữ liệu đọc lại.";
        var readBackResponse = await PostTurnAsync(new AiAssistantTurnRequestDto(
            p10,
            new AiAssistantClientContextDto(
                $"/projects/{projectId:D}", "project", projectId, "project", projectId,
                OrganizationId: organizationId)));
        readBackResponse.StatusCode.Should().Be(HttpStatusCode.OK, await readBackResponse.Content.ReadAsStringAsync());
        var readBack = (await readBackResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;

        readBack.Intent.Should().Be(AiProjectOrchestrationContract.MonitorCapabilityId);
        readBack.ProjectLaunchPlan.Should().NotBeNull();
        readBack.ProjectLaunchPlan!.ExecutionReceipt!.ReceiptId.Should().Be(confirmed.ExecutionReceipt.ReceiptId);
        readBack.ProjectLaunchPlan.LatestReplanProposal.Should().BeNull();
        readBack.AssistantMessage.Should().Contain("không phát hiện bản ghi tạo trùng");

        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await afterDb.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"))).Should().Be(projectCount);
        (await afterDb.Set<Sprint>().CountAsync(item => item.ProjectId == projectId)).Should().Be(sprintCount);
        (await afterDb.TaskItems.CountAsync(item => item.ProjectId == projectId)).Should().Be(taskCount);
        (await afterDb.TaskDependencies.CountAsync(item => item.Predecessor.ProjectId == projectId)).Should().Be(dependencyCount);
    }

    [Fact]
    [Trait("TestId", "TEST-PL-C-STALE-01")]
    public async Task ProjectLaunchConfirm_WhenCapacityChanges_FailsClosedWithoutCreatingProject()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var profile = await db.OrganizationMemberCapacityProfiles.SingleAsync(item => item.OrganizationId == organizationId);
            profile.WeeklyCapacityHours = 1;
            await db.SaveChangesAsync();
        }

        var scenarioId = plan.StaffingScenarios.First(item => item.Feasible).ScenarioId;
        var response = await SendLaunchCommandResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm",
            new ConfirmProjectLaunchPlanRequestDto(true, plan.RowRevision, scenarioId),
            $"integration-stale-{plan.PlanId:N}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await afterDb.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"))).Should().Be(0);
        (await afterDb.ProjectLaunchExecutions.CountAsync(item => item.ProjectLaunchPlanArtifactId == plan.PlanId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-PL-REVIEW-BOUNDS-01")]
    public async Task ProjectLaunchPlan_RejectsInvalidSprintAndPersistsActionableCapacityBlockers()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenario = plan.StaffingScenarios.First(item => item.Feasible);
        var staffing = scenario.Members.Select(member => new ProjectStaffingOverrideDto(
            member.UserId, member.ProposedRole, member.ProposedHours, true, member.UserId == scenario.ManagerUserId)).ToArray();
        var firstSprint = plan.DeliveryPlan.Sprints[0];
        var overlapping = plan.DeliveryPlan.Sprints.Append(firstSprint with
        {
            ClientId = $"overlap-{Guid.NewGuid():N}",
            Name = "Sprint trùng thời gian",
            Tasks = []
        }).ToArray();
        var overlapResponse = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}",
            new UpdateProjectLaunchPlanRequestDto(plan.RowRevision, scenario.ScenarioId, staffing, overlapping));
        overlapResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await overlapResponse.Content.ReadAsStringAsync()).Should().Contain("project_launch_sprint_invalid");

        var impossibleTaskHours = (int)Math.Ceiling(scenario.Members.Max(item => item.ProposedHours)) + 1;
        var overloaded = plan.DeliveryPlan.Sprints.Select((sprint, sprintIndex) => sprint with
        {
            Tasks = sprint.Tasks.Select((task, taskIndex) => sprintIndex == 0 && taskIndex == 0
                ? task with { EstimatedHours = impossibleTaskHours, ProposedAssigneeId = scenario.ManagerUserId }
                : task).ToArray()
        }).ToArray();
        var overloadResponse = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}",
            new UpdateProjectLaunchPlanRequestDto(
                plan.RowRevision,
                scenario.ScenarioId,
                staffing,
                overloaded,
                ProjectLaunchAssignmentModes.PreserveAssignments,
                ProjectLaunchScheduleModes.SequentialSprints));
        overloadResponse.StatusCode.Should().Be(HttpStatusCode.OK, await overloadResponse.Content.ReadAsStringAsync());
        using var overloadDocument = JsonDocument.Parse(await overloadResponse.Content.ReadAsStringAsync());
        var savedOverload = overloadDocument.RootElement.GetProperty("data").Deserialize<ProjectLaunchPlanDto>(JsonOptions)!;
        savedOverload.State.Should().Be("blocked");
        savedOverload.BlockingReasons.Should().Contain(item => item.Contains("vượt allocation", StringComparison.Ordinal));

        var compressedTasks = firstSprint.Tasks.Select(task => task with
        {
            EstimatedHours = 20,
            ProposedAssigneeId = scenario.ManagerUserId,
            ProposedReviewerId = null
        }).ToArray();
        ProjectLaunchSprintPlanDto[] concentrated =
            [firstSprint with { EndDate = firstSprint.StartDate.AddDays(1), Tasks = compressedTasks }];
        var weeklyStaffing = scenario.Members.Select(member => new ProjectStaffingOverrideDto(
            member.UserId,
            member.ProposedRole,
            member.UserId == scenario.ManagerUserId ? 40m : Math.Max(1m, member.ProposedHours),
            true,
            member.UserId == scenario.ManagerUserId)).ToArray();
        var weeklyResponse = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}",
            new UpdateProjectLaunchPlanRequestDto(
                savedOverload.RowRevision,
                scenario.ScenarioId,
                weeklyStaffing,
                concentrated,
                ProjectLaunchAssignmentModes.PreserveAssignments,
                ProjectLaunchScheduleModes.SequentialSprints));
        weeklyResponse.StatusCode.Should().Be(HttpStatusCode.OK, await weeklyResponse.Content.ReadAsStringAsync());
        using var weeklyDocument = JsonDocument.Parse(await weeklyResponse.Content.ReadAsStringAsync());
        var savedWeekly = weeklyDocument.RootElement.GetProperty("data").Deserialize<ProjectLaunchPlanDto>(JsonOptions)!;
        savedWeekly.State.Should().Be("blocked");
        savedWeekly.BlockingReasons.Should().Contain(item =>
            item.StartsWith("Capacity tuần ", StringComparison.Ordinal) ||
            item.StartsWith("Ngưỡng sử dụng tuần ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestId", "TEST-RULEBOOK-SCHEMA-01")]
    public async Task RulebookDraft_RejectsUnsupportedAndOutOfRangeRules()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var outOfRange = await SendSessionCommandAsync(
            HttpMethod.Post,
            $"/api/organizations/{organizationId:D}/work-rulebook",
            new CreateOrganizationWorkRuleSetRequestDto([
                new OrganizationWorkRuleDto("max_utilization_percent", "portfolio_capacity", "block", "Giới hạn tải", 140, "percent")
            ]));
        outOfRange.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await outOfRange.Content.ReadAsStringAsync()).Should().Contain("rulebook_value_out_of_range");

        var unsupported = await SendSessionCommandAsync(
            HttpMethod.Post,
            $"/api/organizations/{organizationId:D}/work-rulebook",
            new CreateOrganizationWorkRuleSetRequestDto([
                new OrganizationWorkRuleDto("looks_safe_but_is_not_implemented", "staffing", "block", "Không được giả lập")
            ]));
        unsupported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unsupported.Content.ReadAsStringAsync()).Should().Contain("rulebook_rule_unsupported");

        var excessiveReviewOverhead = await SendSessionCommandAsync(
            HttpMethod.Post,
            $"/api/organizations/{organizationId:D}/work-rulebook",
            new CreateOrganizationWorkRuleSetRequestDto([
                new OrganizationWorkRuleDto("reviewer_coordination_overhead_percent", "portfolio_capacity", "block", "Chi phí review", 75, "percent")
            ]));
        excessiveReviewOverhead.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await excessiveReviewOverhead.Content.ReadAsStringAsync()).Should().Contain("rulebook_value_out_of_range");
    }

    [Fact]
    [Trait("TestId", "TEST-GS-01")]
    public async Task BroadDemoRequest_ReturnsUsefulGuidanceWithoutExecution()
    {
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Bạn hãy tự phân tích rồi chạy test demo tất cả CAND đã implement",
            new AiAssistantClientContextDto("/dashboard", "workspace")));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn!.Disposition.Should().Be("guided_answer");
        turn.Intent.Should().Be(AiAssistantTurnContract.GuidedAnswerIntent);
        turn.ExecutionPolicy.Should().Be("analyze_only");
        turn.Artifact.Should().BeNull();
        turn.Answer.Should().NotBeNull();
        turn.Answer!.Model.Should().NotBeNull();
        turn.Answer.Model!.Provider.Should().Be(turn.ActualProvider,
            "provider/model metadata must describe the response that was actually rendered, including server fallback");
        turn.AssistantMessage.Should().Contain("unit, integration và E2E");
        turn.AssistantMessage.Should().NotContain("adapter");
        turn.GoalAnalysis!.Objective.Should().Contain("test demo");
        turn.GoalAnalysis.MissingSkills.Should().Contain(item => item.SkillId == "demo.test.run.v1");
        turn.WorkPlan!.SelectedSkillIds.Should().BeEmpty();
        turn.WorkPlan.Steps.Should().NotContain(item => item.Kind == "call_skill");
    }

    [Fact]
    [Trait("TestId", "TEST-GS-02")]
    public async Task NaturalDemoExecutionWording_IsPolicyRoutedWithoutProviderHandoffFailure()
    {
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "chạy tự động để test các CAND đã implement",
            new AiAssistantClientContextDto("/dashboard", "workspace")));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn!.Disposition.Should().Be("guided_answer");
        turn.Intent.Should().Be(AiAssistantTurnContract.GuidedAnswerIntent);
        turn.ExecutionPolicy.Should().Be("analyze_only");
        turn.Artifact.Should().BeNull();
        turn.ActualProvider.Should().Be("DeepSeek");
        turn.ActualModel.Should().Be("deepseek-chat");
        turn.AssistantMessage.Should().Contain("kế hoạch kiểm chứng");
        turn.GoalAnalysis!.ActualProvider.Should().Be("Qaly policy router");
        turn.GoalAnalysis.MissingSkills.Should().ContainSingle(item => item.SkillId == "demo.test.run.v1");
        turn.WorkPlan!.SelectedSkillIds.Should().BeEmpty();
        turn.WorkPlan.Steps.Should().NotContain(item => item.Kind == "call_skill");
        turn.ProcessEvents.Should().Contain(item => item.Stage == "capability_handoff" && item.Status == "completed");
        turn.ProcessEvents.Should().NotContain(item => item.Status == "failed");
    }

    [Fact]
    [Trait("TestId", "TEST-AS-01")]
    [Trait("TestId", "TEST-AS-04")]
    public async Task CompletedTurn_IsPersistedWithOrderedProcessEvents_AndReloadsFromServer()
    {
        var projectId = await SeedOwnedProjectAsync();
        var session = await CreateSessionAsync();
        var request = new AiAssistantTurnRequestDto(
            "Tạo task frontend và QA cho luồng đăng nhập",
            new AiAssistantClientContextDto("/dashboard", "project", projectId, "project", projectId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid());

        var response = await SendTurnAsync(request, $"assistant-test-{Guid.NewGuid():N}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        completed.Should().NotBeNull();
        completed!.SessionVersion.Should().Be(1);
        completed.Sequence.Should().Be(1);
        completed.ProcessEvents.Should().HaveCount(5);
        completed.ProcessEvents!.Select(item => item.Sequence).Should().BeInAscendingOrder();
        completed.ProcessEvents.Select(item => item.Stage).Should().Contain("goal_analysis");
        completed.GoalAnalysis!.SelectedSkills.Should().ContainSingle(item =>
            item.SkillId == AiAssistantContextContract.TaskCreateCapability);
        completed.WorkPlan!.Steps.Should().Contain(item => item.Kind == "call_skill" && item.State == "completed");

        var restored = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{session.SessionId:D}",
            JsonOptions);
        restored.Should().NotBeNull();
        restored!.Version.Should().Be(1);
        restored.Turns.Should().ContainSingle();
        restored.Turns[0].UserMessage.Should().Be(request.Message);
        restored.Turns[0].Status.Should().Be("completed");
        restored.Turns[0].Response!.Artifact.Should().NotBeNull();
        restored.Turns[0].ProcessEvents.Should().HaveCount(5);
        restored.Turns[0].Response!.Capabilities.Should().Contain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability);
        restored.Turns[0].Response!.SourceDisclosures.Should().Contain(item => item.Status == "read");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var auditTypes = await db.AiAuditEvents
            .Where(item => item.EntityGuid == restored.Turns[0].TurnId)
            .Select(item => item.EventType)
            .ToListAsync();
        auditTypes.Should().Contain("assistant_turn.accepted");
        auditTypes.Should().Contain("assistant_context.resolved");
        auditTypes.Should().Contain("assistant_goal.planned");
        auditTypes.Should().Contain("assistant_turn.completed");
    }

    [Fact]
    [Trait("TestId", "TEST-RP-01")]
    [Trait("TestId", "TEST-RP-02")]
    public async Task ResearchPlan_IsGroundedReconciledAndReloadableWithoutMutation()
    {
        var projectId = await SeedOwnedProjectAsync();
        var session = await CreateSessionAsync();
        var request = new AiAssistantTurnRequestDto(
            "Phân tích rủi ro và đề xuất phương án xử lý",
            new AiAssistantClientContextDto("/dashboard", "project", projectId, "project", projectId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid());

        var response = await SendTurnAsync(request, $"assistant-research-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Disposition.Should().Be("research_plan");
        turn.ExecutionPolicy.Should().Be("read_only_proposal");
        turn.ResearchPlan.Should().NotBeNull();
        turn.ResearchPlan!.Findings.Should().OnlyContain(item =>
            item.SourceRefs.All(sourceRef => turn.SourceRefs.Contains(sourceRef)));
        turn.ResearchPlan.ProposedActions.Single(item => item.CapabilityId == "task.create.v1")
            .ExecutionEligible.Should().BeTrue();
        turn.ResearchPlan.ProposedActions.Single(item => item.CapabilityId == "project.create.v1")
            .ExecutionEligible.Should().BeFalse();
        turn.ResearchPlan.ActualProvider.Should().Be("IntegrationProvider");
        turn.ActualProvider.Should().Be("IntegrationProvider");

        var restored = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{session.SessionId:D}",
            JsonOptions);
        restored!.Turns.Should().ContainSingle();
        restored.Turns[0].Response!.ResearchPlan!.SchemaId.Should().Be(AiAssistantResearchPlanContract.SchemaId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskItems.CountAsync(item => item.ProjectId == projectId)).Should().Be(0);
        (await db.AssistantArtifactRefs.AnyAsync(item =>
            item.TurnId == restored.Turns[0].TurnId &&
            item.RendererId == AiAssistantResearchPlanContract.RendererId)).Should().BeTrue();
        var auditTypes = await db.AiAuditEvents
            .Where(item => item.EntityGuid == restored.Turns[0].TurnId)
            .Select(item => item.EventType)
            .ToListAsync();
        auditTypes.Should().Contain("assistant_context.resolved");
        auditTypes.Should().Contain("assistant_turn.completed");
    }

    [Fact]
    [Trait("TestId", "TEST-ACR-01")]
    public async Task UnknownCapability_IsRejectedBeforeTurnPersistence()
    {
        var session = await CreateSessionAsync();
        var request = new AiAssistantTurnRequestDto(
            "Phân tích workspace",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: "shell.execute.v1");

        var response = await SendTurnAsync(request, $"assistant-unknown-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AssistantTurns.CountAsync(item => item.SessionId == session.SessionId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-ACR-02")]
    public async Task TaskCreate_ForReadOnlyProjectMember_ReturnsGuidanceButNoArtifactOrMutation()
    {
        var projectId = await SeedForeignProjectAsync(addCurrentUserAsViewer: true);
        int taskCountBefore;
        using (var beforeScope = _factory.Services.CreateScope())
        {
            taskCountBefore = await beforeScope.ServiceProvider
                .GetRequiredService<QalyDbContext>()
                .TaskItems.CountAsync();
        }
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Tạo task backend cho dự án",
            new AiAssistantClientContextDto("/dashboard", "project_tasks", projectId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Disposition.Should().Be("guided_answer");
        turn.Intent.Should().Be(AiAssistantTurnContract.GuidedAnswerIntent);
        turn.ExecutionPolicy.Should().Be("analyze_only");
        turn.Artifact.Should().BeNull();
        turn.ActualProvider.Should().Be("DeepSeek");
        turn.AssistantMessage.Should().Contain("chưa được phép thực hiện trực tiếp");
        turn.Capabilities.Should().NotContain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability);
        turn.SourceDisclosures.Should().Contain(item =>
            item.Status == "denied" && item.ReasonCode == "capability_not_authorized");
        using var afterScope = _factory.Services.CreateScope();
        (await afterScope.ServiceProvider.GetRequiredService<QalyDbContext>().TaskItems.CountAsync())
            .Should().Be(taskCountBefore);
    }

    [Fact]
    [Trait("TestId", "TEST-ACR-03")]
    public async Task ForeignProjectContext_ReturnsNondisclosingNotFoundWithoutPersistingTurn()
    {
        var projectId = await SeedForeignProjectAsync(addCurrentUserAsViewer: false);
        var session = await CreateSessionAsync();
        var request = new AiAssistantTurnRequestDto(
            "Phân tích dự án",
            new AiAssistantClientContextDto("/dashboard", "project", projectId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid());

        var response = await SendTurnAsync(request, $"assistant-foreign-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AssistantTurns.CountAsync(item => item.SessionId == session.SessionId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-AS-02")]
    public async Task DuplicateClientTurnAndIdempotencyKey_ReplaysSameTurnWithoutAdvancingVersion()
    {
        var session = await CreateSessionAsync();
        var clientTurnId = Guid.NewGuid();
        var key = $"assistant-replay-{Guid.NewGuid():N}";
        var request = new AiAssistantTurnRequestDto(
            "Xin chào",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: clientTurnId);

        var firstResponse = await SendTurnAsync(request, key);
        var first = await firstResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        var replayResponse = await SendTurnAsync(request, key);
        var replay = await replayResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        replay!.Replayed.Should().BeTrue();
        replay.TurnId.Should().Be(first!.TurnId);
        replay.Sequence.Should().Be(1);
        replay.SessionVersion.Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AssistantTurns.CountAsync(item => item.SessionId == session.SessionId)).Should().Be(1);
    }

    [Fact]
    [Trait("TestId", "TEST-AS-03")]
    public async Task StaleVersion_IsRejectedWithoutCreatingAnotherTurn()
    {
        var session = await CreateSessionAsync();
        var first = new AiAssistantTurnRequestDto(
            "Xin chào",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            SessionId: session.SessionId,
            ExpectedVersion: 0,
            ClientTurnId: Guid.NewGuid());
        (await SendTurnAsync(first, $"assistant-first-{Guid.NewGuid():N}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = first with { Message = "Tóm tắt workspace", ClientTurnId = Guid.NewGuid() };
        var staleResponse = await SendTurnAsync(stale, $"assistant-stale-{Guid.NewGuid():N}");
        staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AssistantTurns.CountAsync(item => item.SessionId == session.SessionId)).Should().Be(1);
    }

    [Fact]
    [Trait("TestId", "TEST-RO-LOOP-02")]
    public async Task RunningReadOnlyTurn_CanCancelReloadAndResumeFromDurableSnapshot()
    {
        var projectId = await SeedOwnedProjectAsync();
        var session = await CreateSessionAsync();
        var request = new AiAssistantTurnRequestDto(
            "FORCE_LOOP_DELAY tóm tắt tình trạng dự án",
            new AiAssistantClientContextDto("/dashboard", "project", projectId, "project", projectId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid());

        // Fetch CSRF before starting the in-flight POST. Otherwise the async helper can
        // still be waiting on the CSRF GET while this test polls for the durable turn,
        // which makes a loaded gate look like a persistence failure.
        var turnCsrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var inFlightRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/assistant/turns")
        {
            Content = JsonContent.Create(request)
        };
        inFlightRequest.Headers.Add("X-CSRF-TOKEN", turnCsrf);
        inFlightRequest.Headers.Add("Idempotency-Key", $"assistant-cancel-{Guid.NewGuid():N}");
        inFlightRequest.Headers.Add("X-Request-Id", request.ClientTurnId!.Value.ToString());
        var inFlight = _client.SendAsync(inFlightRequest);
        AssistantTurn? running = null;
        // The integration chat decorator opens a five-second window only after the
        // durable row exists, so this poll verifies persistence rather than relying on
        // incidental provider or thread-pool timing.
        for (var attempt = 0; attempt < 400 && running == null; attempt++)
        {
            await Task.Delay(50);
            using var scope = _factory.Services.CreateScope();
            running = await scope.ServiceProvider.GetRequiredService<QalyDbContext>().AssistantTurns
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.SessionId == session.SessionId && item.Status == "running");
        }
        running.Should().NotBeNull("the durable turn is written before the delayed analysis adapter runs");

        using var cancelRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/ai/assistant/turns/{running!.Id:D}/cancel")
        {
            Content = JsonContent.Create(new AiAssistantTurnControlRequestDto(1))
        };
        cancelRequest.Headers.Add("X-CSRF-TOKEN", turnCsrf);
        var cancelResponse = await _client.SendAsync(cancelRequest);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK, await cancelResponse.Content.ReadAsStringAsync());

        var canceledResponse = await inFlight;
        canceledResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (var canceledScope = _factory.Services.CreateScope())
        {
            var db = canceledScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var canceled = await db.AssistantTurns.AsNoTracking()
                .Include(item => item.ProcessEvents)
                .SingleAsync(item => item.Id == running.Id);
            canceled.Status.Should().Be("canceled");
            canceled.CancellationRequestedAt.Should().NotBeNull();
            canceled.RequestPayloadJson.Should().NotBeNullOrWhiteSpace();
            canceled.ProcessEvents.Should().Contain(item => item.Status == "skipped");
        }

        var resumedClientTurnId = Guid.NewGuid();
        using var resumeRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/ai/assistant/turns/{running.Id:D}/resume")
        {
            Content = JsonContent.Create(new AiAssistantTurnControlRequestDto(1, ClientTurnId: resumedClientTurnId))
        };
        resumeRequest.Headers.Add("X-CSRF-TOKEN", turnCsrf);
        resumeRequest.Headers.Add("Idempotency-Key", $"assistant-resume-{resumedClientTurnId:N}");
        resumeRequest.Headers.Add("X-Request-Id", resumedClientTurnId.ToString());
        var resumeResponse = await _client.SendAsync(resumeRequest);
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await resumeResponse.Content.ReadAsStringAsync());
        var resumed = await resumeResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        resumed!.TurnStatus.Should().Be("completed");
        resumed.SessionVersion.Should().Be(2);

        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var resumedEntity = await finalDb.AssistantTurns.AsNoTracking()
            .SingleAsync(item => item.ClientTurnId == resumedClientTurnId);
        resumedEntity.ResumedFromTurnId.Should().Be(running.Id);
    }

    [Fact]
    [Trait("TestId", "TEST-AS-05")]
    public async Task ForeignUserCannotReadSession_AndReceivesNondisclosingNotFound()
    {
        var session = await CreateSessionAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/ai/assistant/sessions/{session.SessionId:D}");
        request.Headers.Add("X-Test-UserId", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-MULTI-ANSWER-01")]
    public async Task ClarificationDraft_PersistsReloadsAndSubmitsAllAnswersWithoutReasking()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var session = await CreateSessionAsync();
        const string originalMessage = "Khởi chạy dự án web SPA cung cấp dịch vụ theo gói";
        var firstResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            originalMessage,
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId),
            $"assistant-multi-start-{Guid.NewGuid():N}");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
        var first = (await firstResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        first.Conversation!.Questions.Should().HaveCount(3);

        var answers = new[]
        {
            new AiAssistantProgressiveReplyDto("launch.deadline", "12_weeks", "12 tuần"),
            new AiAssistantProgressiveReplyDto("launch.audience", "customer", "Khách hàng"),
            new AiAssistantProgressiveReplyDto("launch.scope", "booking, payment, dashboard", "Đặt dịch vụ, thanh toán, dashboard")
        };
        var draftRequest = new UpdateAiAssistantClarificationDraftRequestDto(
            first.SessionVersion!.Value,
            first.TurnId!.Value,
            originalMessage,
            AiProjectLaunchContract.CapabilityId,
            first.Conversation.Questions,
            answers);
        var draftResponse = await SendSessionCommandAsync(
            HttpMethod.Put,
            $"/api/ai/assistant/sessions/{session.SessionId:D}/clarification-draft",
            draftRequest);
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK, await draftResponse.Content.ReadAsStringAsync());
        var saved = (await draftResponse.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        saved.ClarificationDraft!.Answers.Should().HaveCount(3);

        var reloaded = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{session.SessionId:D}", JsonOptions);
        reloaded!.ClarificationDraft!.Answers.Select(item => item.QuestionId)
            .Should().BeEquivalentTo(answers.Select(item => item.QuestionId));

        var followUpResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            originalMessage,
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: saved.Version,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId,
            ProgressiveReplies: answers),
            $"assistant-multi-submit-{Guid.NewGuid():N}");
        followUpResponse.StatusCode.Should().Be(HttpStatusCode.OK, await followUpResponse.Content.ReadAsStringAsync());
        var followUp = (await followUpResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        followUp.Conversation!.Questions.Should().NotContain(item => answers.Any(answer => answer.QuestionId == item.Id));
        var after = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{session.SessionId:D}", JsonOptions);
        after!.ClarificationDraft.Should().BeNull();
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-P06-P07-LAUNCH-BRIEF-01")]
    public async Task P06P07NaturalLaunchRequest_PreservesNamedBriefAndMapsStructuredScopeAndMetricsWithoutReasking()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var session = await CreateSessionAsync();
        const string p06 = "Khởi chạy Project `E2E-AI-SPA-Dịch-vụ` cho web SPA đặt dịch vụ theo gói. Hãy tái sử dụng dữ kiện tôi đã nói, chỉ hỏi tối đa ba unknown thực sự chặn việc lập phương án. Câu hỏi phải là form/card có option phổ biến và “Khác”, cho phép nhập tự do, lưu nhiều câu trả lời trước khi gửi.";
        var firstResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            p06,
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId),
            $"assistant-p06-{Guid.NewGuid():N}");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
        var first = (await firstResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;

        first.ProjectLaunchBrief.Should().NotBeNull();
        first.ProjectLaunchBrief!.ProposedProjectName.Should().Be("E2E-AI-SPA-Dịch-vụ");
        first.Conversation.Should().NotBeNull();
        first.Conversation!.Questions.Should().HaveCount(3);
        first.Conversation.Questions.Should().OnlyContain(question =>
            question.Blocking && question.AllowFreeText && question.QuickReplies.Count > 0);
        first.Conversation.Questions.Select(question => question.Id).Should().BeEquivalentTo(
            ["launch.deadline", "launch.audience", "launch.scope"]);

        const string p07 = "Thời hạn 12 tuần; người dùng chính là khách hàng cá nhân; bắt buộc có đăng ký/đăng nhập, đặt dịch vụ + phòng, thanh toán và dashboard quản lý. Mục tiêu: 95% luồng đặt dịch vụ E2E pass, p95 API dưới 500ms, không có lỗi Critical khi nghiệm thu.";
        var secondResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            p07,
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: first.SessionVersion,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId),
            $"assistant-p07-{Guid.NewGuid():N}");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK, await secondResponse.Content.ReadAsStringAsync());
        var second = (await secondResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;

        second.ProjectLaunchBrief.Should().NotBeNull();
        second.ProjectLaunchBrief!.Revision.Should().Be(2);
        second.ProjectLaunchBrief.ProposedProjectName.Should().Be("E2E-AI-SPA-Dịch-vụ");
        second.ProjectLaunchBrief.TargetTimebox.Should().Be("12 tuần");
        second.ProjectLaunchBrief.PrimaryAudience.Should().Be("Khách hàng cá nhân");
        second.ProjectLaunchBrief.Scope.Should().BeEquivalentTo(
            ["Đăng ký/đăng nhập", "Đặt dịch vụ + phòng", "Thanh toán", "Dashboard quản lý"]);
        second.ProjectLaunchBrief.ObjectiveProfile!.Metrics.Should().Contain(metric =>
            metric.MetricId == "metric-e2e-pass" && metric.Target == 95 && metric.Unit == "%");
        second.ProjectLaunchBrief.ObjectiveProfile.Metrics.Should().Contain(metric =>
            metric.MetricId == "metric-api-p95" && metric.Target == 500 && metric.Unit == "ms");
        second.ProjectLaunchBrief.ObjectiveProfile.Metrics.Should().Contain(metric =>
            metric.MetricId == "metric-critical-defects" && metric.Target == 0);
        second.Conversation!.Questions.Should().BeEmpty("P07 supplies every blocking product fact in natural language");
        second.ProjectLaunchPlan.Should().NotBeNull("a complete Brief with an effective Rulebook must advance to a reviewable plan");

        const string p08 = "Lập ba phương án manager/team dựa trên skill evidence, capacity đã khai báo, lịch vắng và tải đa dự án. Sau đó chia phase, Sprint, Task, dependency, estimate, required skill và assignee. Cho phép thay người, thêm/bớt người, đổi độ dài Sprint và sửa Task trước xác nhận. Không coi chỗ trống là capacity.";
        var staffingResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            p08,
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: second.SessionVersion,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-p08-{Guid.NewGuid():N}");
        staffingResponse.StatusCode.Should().Be(HttpStatusCode.OK, await staffingResponse.Content.ReadAsStringAsync());
        var staffing = (await staffingResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;

        staffing.Intent.Should().Be(
            AiProjectOrchestrationContract.StaffingCapabilityId,
            "P08 must route to staffing; response was {0}",
            JsonSerializer.Serialize(staffing, JsonOptions));
        staffing.ProjectLaunchPlan.Should().NotBeNull();
        staffing.ProjectLaunchPlan!.StaffingScenarios.Should().HaveCount(3);
        staffing.ProjectLaunchPlan.StaffingScenarios.SelectMany(item => item.ManagerCandidates)
            .Where(candidate => candidate.CapacityState == "missing")
            .Should().OnlyContain(candidate => !candidate.StaffingEligible && candidate.HardRejects.Contains("missing_capacity_profile"));
        var plannedTasks = staffing.ProjectLaunchPlan.DeliveryPlan.Sprints.SelectMany(sprint => sprint.Tasks).ToArray();
        plannedTasks.Should().NotBeEmpty();
        plannedTasks.Should().OnlyContain(task =>
            task.EstimatedHours > 0 &&
            !string.IsNullOrWhiteSpace(task.Description) &&
            task.AcceptanceCriteria.Count > 0 &&
            task.RequiredSkillNames.Count > 0);
        plannedTasks.Should().Contain(task => task.DependencyClientIds.Count > 0);
        var selectedScenario = staffing.ProjectLaunchPlan.StaffingScenarios
            .Single(item => item.ScenarioId == staffing.ProjectLaunchPlan.SelectedScenarioId);
        selectedScenario.RuleDecisions.Should().Contain(item =>
            item.RuleKey == "manager_professional_eligibility" && item.Result == "pass");
        selectedScenario.SourceRefs.Should().Contain(item =>
            item.Contains("member-professional-profile", StringComparison.Ordinal));
        selectedScenario.ManagerCandidates.Should().Contain(item =>
            item.ManagerEligible && item.UserId == selectedScenario.ManagerUserId);
        if (selectedScenario.Feasible)
        {
            plannedTasks.Should().OnlyContain(task => task.ProposedAssigneeId.HasValue,
                "auto-balance must return a real reviewed assignment instead of a wall of unassigned-task blockers");
            plannedTasks.Should().OnlyContain(task => task.ProposedReviewerId != task.ProposedAssigneeId);
        }

        const string p09 = "Dùng phương án đang chọn. Trước khi ghi hãy hiện một card review cuối gồm Project, manager/team, phase, Sprint và tổng số Task. Chờ đúng một xác nhận rõ ràng của tôi.";
        var reviewResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            p09,
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: staffing.SessionVersion,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-p09-{Guid.NewGuid():N}");
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK, await reviewResponse.Content.ReadAsStringAsync());
        var review = (await reviewResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;

        review.Intent.Should().Be(AiProjectOrchestrationContract.ExecuteCapabilityId);
        review.Disposition.Should().Be("confirmation_required");
        review.ExecutionPolicy.Should().Be("explicit_batch_confirm");
        review.ProjectLaunchPlan.Should().NotBeNull("P09 must render the current plan instead of pointing at an older chat message");
        review.ProjectLaunchPlan!.PlanId.Should().Be(staffing.ProjectLaunchPlan.PlanId);
        review.ProjectLaunchPlan.ExecutionReceipt.Should().BeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"))).Should().Be(0,
            "P06/P07 only prepare a reviewable plan and cannot mutate canonical Project data before confirmation");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-LAUNCH-FORM-01")]
    public async Task ProjectLaunchBrief_TypedReviewFormPersistsCustomizationAndUnlocksPlanning()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var session = await CreateSessionAsync();
        var firstResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Khởi chạy một dự án web SPA mới",
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId),
            $"assistant-launch-form-start-{Guid.NewGuid():N}");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
        var first = (await firstResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        first.ProjectLaunchBrief.Should().NotBeNull();

        var formPayload = JsonSerializer.Serialize(new
        {
            projectName = "Qaly SPA Services",
            objective = "Cung cấp dịch vụ SPA theo gói với luồng đặt và thanh toán rõ ràng.",
            targetTimebox = "12 tuần",
            primaryAudience = "Khách hàng",
            objectiveProfile = new
            {
                problemStatement = "Khách hàng đang phải đặt dịch vụ thủ công qua nhiều kênh.",
                primaryAudience = "Khách hàng",
                desiredOutcome = "Khách hàng tự đặt và thanh toán dịch vụ SPA theo gói.",
                businessValue = "Giảm thao tác thủ công và tăng tỷ lệ hoàn tất đơn.",
                metrics = new[]
                {
                    new
                    {
                        metricId = "metric-checkout",
                        title = "Tỷ lệ hoàn tất đặt dịch vụ",
                        metricType = "outcome",
                        baseline = (decimal?)null,
                        target = (decimal?)80,
                        unit = "%",
                        measurementWindow = "30 ngày",
                        dataSource = "Booking funnel",
                        owner = "Product Manager",
                        status = "needs_baseline"
                    }
                },
                guardrails = new List<string> { "Không lưu dữ liệu thanh toán nhạy cảm trong log" },
                assumptions = Array.Empty<string>(),
                nonGoals = new List<string> { "External calendar adapter" }
            },
            features = new[]
            {
                new { featureId = "booking", title = "Đặt dịch vụ", category = "Booking/Scheduling", priority = "must_have", description = "Chọn gói và khung giờ", primaryAudience = "Khách hàng", acceptanceCriteria = new List<string> { "Đặt dịch vụ end-to-end thành công" }, requiredSkillNames = Array.Empty<string>(), selected = true, custom = false },
                new { featureId = "room", title = "Quản lý phòng", category = "Admin/Operations", priority = "must_have", description = "Quản lý phòng phục vụ", primaryAudience = "Nội bộ", acceptanceCriteria = new List<string> { "Không xếp trùng phòng" }, requiredSkillNames = Array.Empty<string>(), selected = true, custom = false },
                new { featureId = "payment", title = "Thanh toán", category = "Billing/Payment", priority = "must_have", description = "Thanh toán và nhận biên nhận", primaryAudience = "Khách hàng", acceptanceCriteria = new List<string> { "Thanh toán có receipt" }, requiredSkillNames = Array.Empty<string>(), selected = true, custom = false },
                new { featureId = "dashboard", title = "Dashboard", category = "Dashboard/Reporting", priority = "out_of_scope", description = "Theo dõi vận hành", primaryAudience = "Quản lý", acceptanceCriteria = new List<string> { "Có chỉ số nguồn rõ ràng" }, requiredSkillNames = Array.Empty<string>(), selected = true, custom = false }
            },
            exclusions = "External calendar adapter"
        }, JsonOptions);
        var reviewedResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Cập nhật Project Launch Brief bằng biểu mẫu đã review.",
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: first.SessionVersion,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId,
            ProgressiveReplies:
            [
                new AiAssistantProgressiveReplyDto(
                    "launch.brief_form",
                    formPayload,
                    "Launch Brief: Qaly SPA Services")
            ]),
            $"assistant-launch-form-submit-{Guid.NewGuid():N}");
        reviewedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await reviewedResponse.Content.ReadAsStringAsync());
        var reviewed = (await reviewedResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;

        reviewed.ProjectLaunchBrief.Should().NotBeNull();
        reviewed.ProjectLaunchBrief!.Revision.Should().Be(2);
        reviewed.ProjectLaunchBrief.ProposedProjectName.Should().Be("Qaly SPA Services");
        reviewed.ProjectLaunchBrief.TargetTimebox.Should().Be("12 tuần");
        reviewed.ProjectLaunchBrief.PrimaryAudience.Should().Be("Khách hàng");
        reviewed.ProjectLaunchBrief.Scope.Should().ContainInOrder("Đặt dịch vụ", "Quản lý phòng", "Thanh toán");
        reviewed.ProjectLaunchBrief.Scope.Should().NotContain("Dashboard");
        reviewed.ProjectLaunchBrief.ObjectiveProfile!.Metrics.Should().ContainSingle(item =>
            item.MetricId == "metric-checkout" && item.Baseline == null && item.Target == 80 && item.Unit == "%");
        reviewed.ProjectLaunchBrief.Features.Should().HaveCount(4);
        reviewed.ProjectLaunchBrief.Features!.Single(item => item.FeatureId == "dashboard").Selected.Should().BeFalse();
        reviewed.ProjectLaunchBrief.Questions.Should().BeEmpty();
        reviewed.ProjectLaunchPlan.Should().NotBeNull("an effective Rulebook plus complete review form should continue to staffing without a dead-end");
        reviewed.ProjectLaunchPlan!.DeliveryPlan.Features.Should().Contain(item => item.FeatureId == "payment");
        reviewed.ProjectLaunchPlan.DeliveryPlan.ObjectiveMetrics.Should().Contain(item => item.MetricId == "metric-checkout");
        reviewed.ProjectLaunchPlan.DeliveryPlan.Sprints.SelectMany(item => item.Tasks)
            .Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.FeatureId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"))).Should().Be(0,
            "the review form only prepares a plan; canonical Project mutation still requires explicit confirmation");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-PLAN-CUSTOMIZE-01")]
    public async Task ProjectLaunchPlan_CustomStaffingAndSprintEditsPersistAndReadBackBeforeExecution()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenario = plan.StaffingScenarios.First(item => item.Feasible);
        var editedSprints = plan.DeliveryPlan.Sprints.Select((sprint, sprintIndex) => sprint with
        {
            Name = sprintIndex == 0 ? "Sprint 1 - Khảo sát và nền tảng" : sprint.Name,
            Tasks = sprint.Tasks.Select((task, taskIndex) => task with
            {
                Title = sprintIndex == 0 && taskIndex == 0 ? "Khảo sát hành trình đặt dịch vụ" : task.Title,
                EstimatedHours = sprintIndex == 0 && taskIndex == 0 ? task.EstimatedHours + 2 : task.EstimatedHours
            }).ToArray()
        }).ToArray();
        var staffing = scenario.Members.Select(member => new ProjectStaffingOverrideDto(
            member.UserId,
            member.ProposedRole,
            member.ProposedHours + (member.UserId == scenario.ManagerUserId ? 2 : 0),
            Included: true,
            Manager: member.UserId == scenario.ManagerUserId)).ToArray();
        var request = new UpdateProjectLaunchPlanRequestDto(
            plan.RowRevision,
            scenario.ScenarioId,
            staffing,
            editedSprints);

        var response = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        var saved = document.RootElement.GetProperty("data").Deserialize<ProjectLaunchPlanDto>(JsonOptions)!;

        saved.RowRevision.Should().Be(plan.RowRevision + 1);
        saved.SelectedScenarioId.Should().Be("custom");
        saved.StaffingScenarios.Single(item => item.ScenarioId == "custom").ManagerUserId
            .Should().Be(scenario.ManagerUserId);
        saved.DeliveryPlan.Sprints[0].Name.Should().Be("Sprint 1 - Khảo sát và nền tảng");
        saved.DeliveryPlan.Sprints[0].Tasks[0].Title.Should().Be("Khảo sát hành trình đặt dịch vụ");
        saved.ExecutionReceipt.Should().BeNull("saving a review must not mutate canonical Project data");

        var reloadResponse = await _client.GetAsync($"/api/ai/project-launch/plans/{plan.PlanId:D}");
        reloadResponse.StatusCode.Should().Be(HttpStatusCode.OK, await reloadResponse.Content.ReadAsStringAsync());
        using var reloadDocument = JsonDocument.Parse(await reloadResponse.Content.ReadAsStringAsync());
        reloadDocument.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        var reloaded = reloadDocument.RootElement.GetProperty("data").Deserialize<ProjectLaunchPlanDto>(JsonOptions)!;
        reloaded.SelectedScenarioId.Should().Be("custom");
        reloaded.DeliveryPlan.Sprints[0].Tasks[0].Title.Should().Be("Khảo sát hành trình đặt dịch vụ");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.Projects.CountAsync(item => item.OrganizationId == organizationId && !item.Code.StartsWith("EV-"))).Should().Be(0,
            "customizing a plan still requires the single explicit Project creation confirmation");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-PLAN-AUTONOMY-01")]
    public async Task ProjectLaunchPlan_PreserveMode_AllowsUnassignedBacklogAndParallelWorkstreamsWithoutSilentAssignment()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenario = plan.StaffingScenarios.First(item => item.Feasible);
        var firstSprint = plan.DeliveryPlan.Sprints[0];
        var parallelStart = firstSprint.StartDate.AddDays(1);
        var parallelEnd = parallelStart.AddDays(Math.Max(1, (firstSprint.EndDate - firstSprint.StartDate).TotalDays - 1));
        if (parallelEnd > plan.DeliveryPlan.EndDate) parallelEnd = plan.DeliveryPlan.EndDate;
        var sourceTasks = firstSprint.Tasks.ToArray();
        sourceTasks.Should().HaveCountGreaterThanOrEqualTo(2);
        var firstWorkstream = firstSprint with
        {
            Name = "Workstream sản phẩm",
            Tasks = sourceTasks.Take(1).Select(task => task with
            {
                ProposedAssigneeId = null,
                ProposedReviewerId = null
            }).ToArray()
        };
        var secondWorkstream = firstSprint with
        {
            ClientId = "workstream-parallel-2",
            Name = "Workstream vận hành",
            StartDate = parallelStart,
            EndDate = parallelEnd,
            Tasks = sourceTasks.Skip(1).Select(task => task with
            {
                ProposedAssigneeId = null,
                ProposedReviewerId = null,
                DependencyClientIds = []
            }).ToArray()
        };
        var reviewedSprints = new[] { firstWorkstream, secondWorkstream };
        var staffing = scenario.Members.Select(member => new ProjectStaffingOverrideDto(
            member.UserId,
            member.ProposedRole,
            1m,
            Included: true,
            Manager: member.UserId == scenario.ManagerUserId)).ToArray();

        staffing.Sum(item => item.ProposedHours).Should().BeLessThan(
            reviewedSprints.SelectMany(item => item.Tasks.Where(task => task.Selected)).Sum(item => item.EstimatedHours),
            "preserve mode must allow deliberately unassigned backlog even when the reviewed team does not cover every backlog hour");

        var updateResponse = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}",
            new UpdateProjectLaunchPlanRequestDto(
                plan.RowRevision,
                scenario.ScenarioId,
                staffing,
                reviewedSprints,
                ProjectLaunchAssignmentModes.PreserveAssignments,
                ProjectLaunchScheduleModes.ParallelWorkstreams));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
        using var updateDocument = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        var saved = updateDocument.RootElement.GetProperty("data").Deserialize<ProjectLaunchPlanDto>(JsonOptions)!;
        saved.DeliveryPlan.AssignmentMode.Should().Be(ProjectLaunchAssignmentModes.PreserveAssignments);
        saved.DeliveryPlan.ScheduleMode.Should().Be(ProjectLaunchScheduleModes.ParallelWorkstreams);
        saved.DeliveryPlan.Sprints.SelectMany(item => item.Tasks.Where(task => task.Selected))
            .Should().OnlyContain(item => item.ProposedAssigneeId == null && item.ProposedReviewerId == null);

        var confirmed = await SendLaunchCommandAsync<ConfirmProjectLaunchPlanRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm",
            new ConfirmProjectLaunchPlanRequestDto(true, saved.RowRevision, saved.SelectedScenarioId!),
            $"integration-preserve-backlog-{plan.PlanId:N}");
        confirmed.ExecutionReceipt!.ReadBackVerified.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var tasks = await db.TaskItems.AsNoTracking()
            .Where(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId)
            .ToListAsync();
        tasks.Should().NotBeEmpty().And.OnlyContain(item => item.AssigneeId == null && item.ReviewerId == null);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-PLAN-AUTOBALANCE-02")]
    public async Task ProjectLaunchPlan_AutoBalance_ReplacesStaleDraftAssignmentsAcrossTheReviewedTeam()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        using (var seedScope = _factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var skills = await db.OrganizationSkills.Where(item => item.OrganizationId == organizationId).ToArrayAsync();
            var evidenceProject = await db.Projects.SingleAsync(item => item.OrganizationId == organizationId && item.Code.StartsWith("EV-"));
            var teammate = new User
            {
                FullName = "Auto Balance Teammate",
                Email = $"{Guid.NewGuid():N}@assistant-autobalance.test",
                PasswordHash = "not-used",
                Role = "User",
                IsActive = true
            };
            var evidenceTask = new TaskItem
            {
                ProjectId = evidenceProject.Id,
                ReporterId = _factory.TestUserId,
                AssigneeId = teammate.Id,
                Title = "Confirmed teammate delivery baseline",
                Description = "Canonical evidence for weekly auto-balance.",
                Status = "Done",
                Priority = "Medium",
                EstimatedHours = 8,
                ActualHours = 8,
                DueDate = DateTimeOffset.UtcNow.AddDays(-2)
            };
            db.AddRange(
                teammate,
                new OrganizationMember { OrganizationId = organizationId, UserId = teammate.Id, Role = OrganizationRoleRules.Member },
                new OrganizationMemberCapacityProfile { OrganizationId = organizationId, UserId = teammate.Id, WeeklyCapacityHours = 40, TimeZoneId = "Asia/Ho_Chi_Minh" },
                evidenceTask);
            db.TaskSkillRequirements.AddRange(skills.Select(skill => new TaskSkillRequirement
            {
                TaskItemId = evidenceTask.Id,
                OrganizationSkillId = skill.Id,
                RequiredLevel = "Intermediate",
                Provenance = "MANUAL",
                ConfirmedByUserId = _factory.TestUserId,
                ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2)
            }));
            db.TaskCompletionAttributions.Add(new TaskCompletionAttribution
            {
                TaskItemId = evidenceTask.Id,
                ContributorUserId = teammate.Id,
                ConfirmedByUserId = _factory.TestUserId,
                CompletedAt = DateTimeOffset.UtcNow.AddDays(-2),
                ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = TaskCompletionAttribution.Confirmed
            });
            await db.SaveChangesAsync();
        }
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenario = plan.StaffingScenarios.First(item => item.Feasible);
        var eligibleCandidates = scenario.ManagerCandidates.Where(item => item.StaffingEligible).ToArray();
        eligibleCandidates.Should().HaveCountGreaterThan(1);
        var staleAssigneeId = scenario.ManagerUserId!.Value;
        var staleAssignments = plan.DeliveryPlan.Sprints.Select(sprint => sprint with
        {
            Tasks = sprint.Tasks.Select(task => task with
            {
                ProposedAssigneeId = staleAssigneeId,
                ProposedReviewerId = null
            }).ToArray()
        }).ToArray();
        var staffing = eligibleCandidates.Select(candidate => new ProjectStaffingOverrideDto(
            candidate.UserId,
            candidate.UserId == scenario.ManagerUserId ? ProjectRoleRules.Manager : ProjectRoleRules.Member,
            Math.Max(1m, candidate.ProposedHours),
            Included: true,
            Manager: candidate.UserId == scenario.ManagerUserId)).ToArray();

        var response = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}",
            new UpdateProjectLaunchPlanRequestDto(
                plan.RowRevision,
                scenario.ScenarioId,
                staffing,
                staleAssignments,
                ProjectLaunchAssignmentModes.AutoBalance,
                ProjectLaunchScheduleModes.SequentialSprints));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var saved = document.RootElement.GetProperty("data").Deserialize<ProjectLaunchPlanDto>(JsonOptions)!;
        var selectedTasks = saved.DeliveryPlan.Sprints
            .Where(sprint => sprint.Selected)
            .SelectMany(sprint => sprint.Tasks.Where(task => task.Selected))
            .ToArray();
        selectedTasks.Should().OnlyContain(task => task.ProposedAssigneeId.HasValue);
        selectedTasks.Select(task => task.ProposedAssigneeId).Distinct().Should().HaveCountGreaterThan(1,
            "auto-balance must not treat assignee ids left by an older scenario as immutable user choices");
        saved.BlockingReasons.Should().NotContain(reason =>
            reason.StartsWith("Capacity tuần ", StringComparison.Ordinal) ||
            reason.StartsWith("Ngưỡng sử dụng tuần ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-PLAN-INTEGRITY-01")]
    public async Task ProjectLaunchPlan_RejectsPrivilegedRoleAndForeignOrganizationSkill()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenario = plan.StaffingScenarios.First(item => item.Feasible);
        var invalidRoleStaffing = scenario.Members.Select(member => new ProjectStaffingOverrideDto(
            member.UserId,
            member.UserId == scenario.ManagerUserId ? ProjectRoleRules.Owner : member.ProposedRole,
            member.ProposedHours,
            true,
            false)).ToArray();
        var roleResponse = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}",
            new UpdateProjectLaunchPlanRequestDto(plan.RowRevision, scenario.ScenarioId, invalidRoleStaffing, plan.DeliveryPlan.Sprints));
        roleResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await roleResponse.Content.ReadAsStringAsync()).Should().Contain("project_launch_role_invalid");

        Guid foreignSkillId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var foreignOrganization = new Organization
            {
                Name = $"Foreign Skill Org {Guid.NewGuid():N}",
                Code = $"FS-{Guid.NewGuid():N}"[..12],
                OwnerId = _factory.TestUserId,
                IsActive = true
            };
            var foreignSkill = new OrganizationSkill
            {
                OrganizationId = foreignOrganization.Id,
                Name = "Foreign-only skill",
                NormalizedName = "foreign-only skill",
                Category = "Chuyên môn",
                DefaultRequiredLevel = "Intermediate",
                IsActive = true
            };
            db.AddRange(foreignOrganization, foreignSkill);
            await db.SaveChangesAsync();
            foreignSkillId = foreignSkill.Id;
        }

        var editedSprints = plan.DeliveryPlan.Sprints.Select((sprint, sprintIndex) => sprint with
        {
            Tasks = sprint.Tasks.Select((task, taskIndex) => sprintIndex == 0 && taskIndex == 0
                ? task with { RequiredSkillIds = [foreignSkillId] }
                : task).ToArray()
        }).ToArray();
        var validStaffing = scenario.Members.Select(member => new ProjectStaffingOverrideDto(
            member.UserId,
            member.ProposedRole,
            member.ProposedHours,
            true,
            member.UserId == scenario.ManagerUserId)).ToArray();
        var skillResponse = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}",
            new UpdateProjectLaunchPlanRequestDto(plan.RowRevision, scenario.ScenarioId, validStaffing, editedSprints));
        skillResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await skillResponse.Content.ReadAsStringAsync()).Should().Contain("project_launch_skill_invalid");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-PLAN-INTEGRITY-02")]
    public async Task ProjectLaunchPlan_PreservesReviewedAssigneeAndUpdatesDurableSessionBeforeConfirm()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        Guid secondUserId;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var secondUser = new User
            {
                FullName = "Reviewed Assignee",
                Email = $"{Guid.NewGuid():N}@launch-assignee.test",
                PasswordHash = "not-used",
                Role = "User",
                IsActive = true
            };
            db.Users.Add(secondUser);
            db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = organizationId,
                UserId = secondUser.Id,
                Role = OrganizationRoleRules.Member
            });
            db.OrganizationMemberCapacityProfiles.Add(new OrganizationMemberCapacityProfile
            {
                OrganizationId = organizationId,
                UserId = secondUser.Id,
                WeeklyCapacityHours = 40,
                TimeZoneId = "Asia/Ho_Chi_Minh"
            });
            await db.SaveChangesAsync();
            secondUserId = secondUser.Id;
        }

        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenario = plan.StaffingScenarios
            .Where(item => item.Feasible && item.Members.Any(member => member.UserId == secondUserId))
            .OrderByDescending(item => item.Members.Count)
            .First();
        var editedTitle = "Công việc giữ nguyên người được review";
        var editedSprints = plan.DeliveryPlan.Sprints.Select((sprint, sprintIndex) => sprint with
        {
            Tasks = sprint.Tasks.Select((task, taskIndex) => sprintIndex == 0 && taskIndex == 0
                ? task with
                {
                    Title = editedTitle,
                    ProposedAssigneeId = secondUserId,
                    ProposedReviewerId = _factory.TestUserId,
                    AcceptanceCriteria = ["Người dùng hoàn tất được luồng chính"],
                    DefinitionOfDone = ["Đã kiểm thử và cập nhật tài liệu"]
                }
                : task).ToArray()
        }).ToArray();
        var totalHours = editedSprints.Where(item => item.Selected).SelectMany(item => item.Tasks.Where(task => task.Selected)).Sum(item => item.EstimatedHours);
        var staffing = scenario.Members.Select(member => new ProjectStaffingOverrideDto(
            member.UserId,
            member.ProposedRole,
            member.UserId == secondUserId ? Math.Max(member.ProposedHours, totalHours) : member.ProposedHours,
            true,
            member.UserId == scenario.ManagerUserId)).ToArray();
        var updateResponse = await SendPlanUpdateResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}",
            new UpdateProjectLaunchPlanRequestDto(plan.RowRevision, scenario.ScenarioId, staffing, editedSprints));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
        using var updateDocument = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        var saved = updateDocument.RootElement.GetProperty("data").Deserialize<ProjectLaunchPlanDto>(JsonOptions)!;
        saved.DeliveryPlan.Sprints.SelectMany(item => item.Tasks).Single(item => item.Title == editedTitle)
            .ProposedAssigneeId.Should().Be(secondUserId);

        Guid sessionId;
        Guid assistantTurnId;
        using (var lookupScope = _factory.Services.CreateScope())
        {
            var db = lookupScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var artifact = await db.ProjectLaunchPlanArtifacts.AsNoTracking().SingleAsync(item => item.Id == plan.PlanId);
            sessionId = artifact.AssistantSessionId;
            assistantTurnId = artifact.AssistantTurnId;
        }
        var session = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{sessionId:D}", JsonOptions);
        session!.Turns.Single(item => item.TurnId == assistantTurnId).Response!.ProjectLaunchPlan!
            .DeliveryPlan.Sprints.SelectMany(item => item.Tasks).Should().Contain(item => item.Title == editedTitle);

        var confirmed = await SendLaunchCommandAsync<ConfirmProjectLaunchPlanRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm",
            new ConfirmProjectLaunchPlanRequestDto(true, saved.RowRevision, saved.SelectedScenarioId!),
            $"integration-reviewed-assignee-{plan.PlanId:N}");
        using var confirmScope = _factory.Services.CreateScope();
        var confirmDb = confirmScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var persistedTask = await confirmDb.TaskItems.SingleAsync(item =>
            item.ProjectId == confirmed.ExecutionReceipt!.ProjectId && item.Title == editedTitle);
        persistedTask.AssigneeId.Should().Be(secondUserId,
            "confirmation must apply the reviewed assignment instead of silently balancing again");
        persistedTask.ReviewerId.Should().Be(_factory.TestUserId);
        persistedTask.Description.Should().NotContain("Acceptance criteria",
            "acceptance and Definition of Done belong to canonical checklist rows, not flattened description text");
        var checklist = await confirmDb.TaskAcceptanceChecklistItems
            .Where(item => item.TaskId == persistedTask.Id)
            .OrderBy(item => item.SortOrder)
            .ToListAsync();
        checklist.Should().ContainSingle(item => item.Kind == TaskAcceptanceChecklistItem.Acceptance &&
            item.Text == "Người dùng hoàn tất được luồng chính");
        checklist.Should().ContainSingle(item => item.Kind == TaskAcceptanceChecklistItem.DefinitionOfDone &&
            item.Text == "Đã kiểm thử và cập nhật tài liệu");
        confirmed.ExecutionReceipt!.ReadBackVerified.Should().BeTrue(
            "receipt is successful only after semantic read-back verifies reviewer and checklist data");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-PLAN-INTEGRITY-03")]
    public async Task ProjectLaunchConfirm_WhenSkillCatalogChanges_FailsClosed()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        Guid skillId;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var skill = new OrganizationSkill
            {
                OrganizationId = organizationId,
                Name = "Audit-only catalog skill",
                NormalizedName = "audit-only catalog skill",
                Category = "Nền tảng",
                DefaultRequiredLevel = "Intermediate",
                IsActive = true
            };
            db.OrganizationSkills.Add(skill);
            await db.SaveChangesAsync();
            skillId = skill.Id;
        }
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        using (var changeScope = _factory.Services.CreateScope())
        {
            var db = changeScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var skill = await db.OrganizationSkills.SingleAsync(item => item.Id == skillId);
            skill.Category = "Đã thay đổi sau planning";
            await db.SaveChangesAsync();
        }
        var scenarioId = plan.StaffingScenarios.First(item => item.Feasible).ScenarioId;
        var response = await SendLaunchCommandResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm",
            new ConfirmProjectLaunchPlanRequestDto(true, plan.RowRevision, scenarioId),
            $"integration-skill-stale-{plan.PlanId:N}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("project_launch_sources_stale");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-ROLLBACK-PRISTINE-01")]
    public async Task ProjectLaunchRollback_WhenLaunchIsPristine_RollsBackCanonicalProject()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenarioId = plan.StaffingScenarios.First(item => item.Feasible).ScenarioId;
        var confirmed = await SendLaunchCommandAsync<ConfirmProjectLaunchPlanRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm",
            new ConfirmProjectLaunchPlanRequestDto(true, plan.RowRevision, scenarioId),
            $"integration-pristine-confirm-{plan.PlanId:N}");
        var rolledBack = await SendLaunchCommandAsync<RollbackProjectLaunchExecutionRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/executions/{confirmed.ExecutionReceipt!.ReceiptId:D}/rollback",
            new RollbackProjectLaunchExecutionRequestDto(true, confirmed.ExecutionReceipt.Revision, "Pristine integration rollback"),
            $"integration-pristine-rollback-{confirmed.ExecutionReceipt.ReceiptId:N}");
        rolledBack.State.Should().Be("rolled_back");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.Projects.IgnoreQueryFilters().SingleAsync(item => item.Id == confirmed.ExecutionReceipt.ProjectId))
            .IsDeleted.Should().BeTrue();
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-ROLLBACK-IMPACT-01")]
    public async Task ProjectLaunchRollback_WhenUntracedTodoWasAdded_BlocksWithoutDeletingIt()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        var plan = await CreateProjectLaunchPlanAsync(organizationId);
        var scenarioId = plan.StaffingScenarios.First(item => item.Feasible).ScenarioId;
        var confirmed = await SendLaunchCommandAsync<ConfirmProjectLaunchPlanRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm",
            new ConfirmProjectLaunchPlanRequestDto(true, plan.RowRevision, scenarioId),
            $"integration-impact-confirm-{plan.PlanId:N}");
        Guid manualTaskId;
        using (var addScope = _factory.Services.CreateScope())
        {
            var db = addScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var manualTask = new TaskItem
            {
                ProjectId = confirmed.ExecutionReceipt!.ProjectId,
                Title = "Công việc người dùng thêm sau khi tạo dự án",
                Description = "Không được rollback xóa âm thầm.",
                Status = "Todo",
                Priority = "Medium",
                ReporterId = _factory.TestUserId
            };
            db.TaskItems.Add(manualTask);
            await db.SaveChangesAsync();
            manualTaskId = manualTask.Id;
        }
        var rollbackResponse = await SendLaunchCommandResponseAsync(
            $"/api/ai/project-launch/executions/{confirmed.ExecutionReceipt!.ReceiptId:D}/rollback",
            new RollbackProjectLaunchExecutionRequestDto(true, confirmed.ExecutionReceipt.Revision, "Rollback must detect manual todo"),
            $"integration-impact-rollback-{confirmed.ExecutionReceipt.ReceiptId:N}");
        rollbackResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.TaskItems.IgnoreQueryFilters().SingleAsync(item => item.Id == manualTaskId)).IsDeleted.Should().BeFalse();
        (await verifyDb.Projects.IgnoreQueryFilters().SingleAsync(item => item.Id == confirmed.ExecutionReceipt.ProjectId)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-HISTORY-01")]
    public async Task SessionHistory_CanListRenameArchiveAndSoftDeleteWithVersionChecks()
    {
        var session = await CreateSessionAsync();
        var listed = await _client.GetFromJsonAsync<List<AiAssistantSessionSummaryDto>>(
            "/api/ai/assistant/sessions?includeArchived=true", JsonOptions);
        listed.Should().Contain(item => item.SessionId == session.SessionId);

        var rename = await SendSessionCommandAsync(
            HttpMethod.Patch,
            $"/api/ai/assistant/sessions/{session.SessionId:D}",
            new UpdateAiAssistantSessionRequestDto(session.Version, "SPA launch conversation"));
        var renamed = (await rename.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        renamed.Title.Should().Be("SPA launch conversation");

        var archive = await SendSessionCommandAsync(
            HttpMethod.Post,
            $"/api/ai/assistant/sessions/{session.SessionId:D}/archive",
            new AiAssistantSessionControlRequestDto(renamed.Version));
        var archived = (await archive.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        archived.Status.Should().Be("archived");

        var delete = await SendSessionCommandAsync<object>(
            HttpMethod.Delete,
            $"/api/ai/assistant/sessions/{session.SessionId:D}?expectedVersion={archived.Version}",
            body: null);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var finalList = await _client.GetFromJsonAsync<List<AiAssistantSessionSummaryDto>>(
            "/api/ai/assistant/sessions?includeArchived=true", JsonOptions);
        finalList.Should().NotContain(item => item.SessionId == session.SessionId);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P12-P13-ASSIGNMENT-CARD-01")]
    public async Task P12P13_AssignmentProposalUsesCanonicalEvidenceAndReopensSameUnmutatedDraft()
    {
        var seeded = await SeedAssistantAssignmentTaskAsync();
        var context = new AiAssistantClientContextDto(
            $"/projects/{seeded.ProjectId:D}/tasks/{seeded.TaskId:D}",
            "task", seeded.ProjectId, "task", seeded.TaskId);
        var session = await CreateSessionAsync();
        var firstResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Với Task đang mở, hãy lập phương án giao việc và lịch. Đối chiếu required skill, evidence đã xác nhận, capacity thật, availability, deadline và tải ở tất cả Project; cho tôi đổi ứng viên hoặc ngày trước khi xác nhận.",
            context,
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-p12-{Guid.NewGuid():N}");

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
        var first = (await firstResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        first.Intent.Should().Be(AiAssistantContextContract.TaskAssignmentScheduleCapability);
        first.Disposition.Should().Be("assignment_schedule_proposal");
        first.ExecutionPolicy.Should().Be("explicit_single_confirm");
        first.ActualProvider.Should().NotBe("not_reached");
        first.PortfolioScheduleProposal.Should().NotBeNull();
        var proposal = first.PortfolioScheduleProposal!;
        proposal.ProviderName.Should().Be("LocalRules");
        proposal.Status.Should().Be(AiDraftStatuses.PendingReview);
        proposal.Items.Should().ContainSingle();
        proposal.Items[0].SkillCoveragePercent.Should().BeGreaterThan(0);
        proposal.Items[0].EvidenceConfidence.Should().BeGreaterThan(0);
        proposal.Items[0].Alternatives.Should().NotBeEmpty();
        proposal.Sources.Should().Contain(source => source.Type == "task");
        proposal.Sources.Should().Contain(source => source.Type == "member_capacity");

        var secondResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Giữ phương án hiện tại và cho tôi card xác nhận cuối. Không ghi trước khi tôi bấm xác nhận.",
            context,
            SessionId: first.SessionId,
            ExpectedVersion: first.SessionVersion,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-p13-{Guid.NewGuid():N}");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK, await secondResponse.Content.ReadAsStringAsync());
        var second = (await secondResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        second.Intent.Should().Be(AiAssistantContextContract.TaskAssignmentScheduleCapability);
        second.PortfolioScheduleProposal.Should().NotBeNull();
        second.PortfolioScheduleProposal!.DraftId.Should().Be(proposal.DraftId);
        second.PortfolioScheduleProposal.Status.Should().Be(AiDraftStatuses.PendingReview);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var unchanged = await verifyDb.TaskItems.AsNoTracking().SingleAsync(item => item.Id == seeded.TaskId);
        unchanged.AssigneeId.Should().BeNull("P12/P13 only review the proposal before the card confirmation");
        unchanged.StartDate.Should().BeNull();
        (await verifyDb.TaskAssignments.CountAsync(item => item.TaskItemId == seeded.TaskId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-P04-P05-SCOPE-01")]
    public async Task SessionScope_ChangesInPlaceAndSurvivesReloadWithoutLosingTurns()
    {
        var projectId = await SeedOwnedProjectAsync();
        var session = await CreateSessionAsync();
        var turnResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Bạn có thể giúp tôi những gì?",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid()),
            $"assistant-scope-history-{Guid.NewGuid():N}");
        turnResponse.StatusCode.Should().Be(HttpStatusCode.OK, await turnResponse.Content.ReadAsStringAsync());
        var completed = (await turnResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;

        var scopeResponse = await SendSessionCommandAsync(
            HttpMethod.Put,
            $"/api/ai/assistant/sessions/{session.SessionId:D}/scope",
            new UpdateAiAssistantSessionScopeRequestDto(completed.SessionVersion!.Value, projectId));
        scopeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await scopeResponse.Content.ReadAsStringAsync());
        var scoped = (await scopeResponse.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        scoped.SessionId.Should().Be(session.SessionId);
        scoped.ProjectId.Should().Be(projectId);
        scoped.Turns.Should().ContainSingle();
        scoped.Version.Should().Be(completed.SessionVersion.Value + 1);

        var restored = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{session.SessionId:D}", JsonOptions);
        restored!.ProjectId.Should().Be(projectId);
        restored.Turns.Should().ContainSingle(turn => turn.UserMessage == "Bạn có thể giúp tôi những gì?");

        var staleResponse = await SendSessionCommandAsync(
            HttpMethod.Put,
            $"/api/ai/assistant/sessions/{session.SessionId:D}/scope",
            new UpdateAiAssistantSessionScopeRequestDto(completed.SessionVersion.Value, null));
        staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var clearResponse = await SendSessionCommandAsync(
            HttpMethod.Put,
            $"/api/ai/assistant/sessions/{session.SessionId:D}/scope",
            new UpdateAiAssistantSessionScopeRequestDto(scoped.Version, null));
        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK, await clearResponse.Content.ReadAsStringAsync());
        var cleared = (await clearResponse.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        cleared.SessionId.Should().Be(session.SessionId);
        cleared.ProjectId.Should().BeNull();
        cleared.Turns.Should().ContainSingle();
    }

    private async Task<HttpResponseMessage> PostTurnAsync(AiAssistantTurnRequestDto request)
    {
        var session = await CreateSessionAsync();
        return await SendTurnAsync(
            request with
            {
                SessionId = session.SessionId,
                ExpectedVersion = session.Version,
                ClientTurnId = Guid.NewGuid()
            },
            $"assistant-test-{Guid.NewGuid():N}");
    }

    private static Dictionary<string, object?> TelemetryTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => tags.ToArray().ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private async Task<AiAssistantSessionDto> CreateSessionAsync()
    {
        var csrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/ai/assistant/sessions")
        {
            Content = JsonContent.Create(new CreateAiAssistantSessionRequestDto(
                new AiAssistantClientContextDto("/dashboard", "workspace"),
                "Assistant integration test"))
        };
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        var response = await _client.SendAsync(message);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> SendTurnAsync(
        AiAssistantTurnRequestDto request,
        string idempotencyKey)
    {
        var csrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/ai/assistant/turns")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Headers.Add("X-Request-Id", request.ClientTurnId?.ToString() ?? Guid.NewGuid().ToString());
        return await _client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> SendSessionCommandAsync<T>(HttpMethod method, string url, T? body)
    {
        var csrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var message = new HttpRequestMessage(method, url);
        if (body != null) message.Content = JsonContent.Create(body);
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await _client.SendAsync(message);
    }

    private async Task<ProjectLaunchPlanDto> CreateProjectLaunchPlanAsync(Guid organizationId)
    {
        var session = await CreateSessionAsync();
        var briefResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Khởi chạy dự án web SPA cho khách hàng trong 8 tuần, bắt buộc có đăng nhập, đặt dịch vụ và thanh toán",
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId),
            $"integration-brief-{Guid.NewGuid():N}");
        briefResponse.StatusCode.Should().Be(HttpStatusCode.OK, await briefResponse.Content.ReadAsStringAsync());
        var briefTurn = (await briefResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        briefTurn.ProjectLaunchBrief.Should().NotBeNull();

        var planResponse = await SendTurnAsync(new AiAssistantTurnRequestDto(
            "Lập staffing scenario và delivery plan từ Launch Brief vừa review",
            new AiAssistantClientContextDto("/dashboard", "workspace", OrganizationId: organizationId),
            SessionId: session.SessionId,
            ExpectedVersion: briefTurn.SessionVersion,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: AiProjectOrchestrationContract.StaffingCapabilityId),
            $"integration-plan-{Guid.NewGuid():N}");
        planResponse.StatusCode.Should().Be(HttpStatusCode.OK, await planResponse.Content.ReadAsStringAsync());
        var planTurn = (await planResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        planTurn.Disposition.Should().Be("project_launch_plan", JsonSerializer.Serialize(planTurn, JsonOptions));
        planTurn.ProjectLaunchPlan.Should().NotBeNull();
        planTurn.ExecutionPolicy.Should().Be("read_only_proposal");
        return planTurn.ProjectLaunchPlan!;
    }

    private async Task<TResponse> SendLaunchCommandAsync<TRequest, TResponse>(
        string url,
        TRequest request,
        string? idempotencyKey = null)
    {
        var response = await SendLaunchCommandResponseAsync(url, request, idempotencyKey);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        return document.RootElement.GetProperty("data").Deserialize<TResponse>(JsonOptions)!;
    }

    private async Task<HttpResponseMessage> SendLaunchCommandResponseAsync<TRequest>(
        string url,
        TRequest request,
        string? idempotencyKey = null)
    {
        var csrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> SendPlanUpdateResponseAsync<TRequest>(string url, TRequest request)
    {
        var csrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var message = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-CSRF-TOKEN", csrf);
        return await _client.SendAsync(message);
    }

    private async Task<Guid> SeedOwnedProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var userId = _factory.TestUserId;
        if (!await db.Users.AnyAsync(item => item.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                FullName = "Assistant Turn Owner",
                Email = $"{userId:N}@assistant-turn.test",
                PasswordHash = "not-used",
                Role = "User",
                IsActive = true
            });
        }

        var project = new Project
        {
            Name = $"Assistant Turn Project {Guid.NewGuid():N}",
            Code = $"AT-{Guid.NewGuid():N}"[..12],
            OwnerId = userId,
            Status = "Active"
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<Guid> SeedRendererNavigationProjectAsync()
    {
        var projectId = await SeedOwnedProjectAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var ownerId = _factory.TestUserId;
        var now = DateTimeOffset.UtcNow;
        var members = Enumerable.Range(1, 3)
            .Select(index => new User
            {
                FullName = $"P27 Member {index}",
                Email = $"p27-member-{index}-{Guid.NewGuid():N}@assistant-turn.test",
                PasswordHash = "not-used",
                Role = "User",
                IsActive = true
            })
            .ToArray();
        await db.Users.AddRangeAsync(members);
        await db.ProjectMembers.AddRangeAsync(members.Select(member => new ProjectMember
        {
            ProjectId = projectId,
            UserId = member.Id,
            Role = "Member"
        }));

        var sprint = new Sprint
        {
            ProjectId = projectId,
            Name = "P27 Sprint có nguy cơ",
            Status = "AtRisk",
            StartDate = now.AddDays(-14),
            EndDate = now.AddDays(-1),
            Goal = "Đóng các blocker còn tồn đọng."
        };
        db.Set<Sprint>().Add(sprint);

        for (var index = 0; index < 6; index++)
        {
            db.TaskItems.Add(new TaskItem
            {
                ProjectId = projectId,
                SprintId = sprint.Id,
                ReporterId = ownerId,
                AssigneeId = members[index % members.Length].Id,
                Title = $"P27 overdue task {index + 1}",
                Description = "Canonical renderer fixture",
                Status = "Todo",
                Priority = index < 3 ? "Critical" : "High",
                DueDate = now.AddDays(-(index + 1)),
                EstimatedHours = 4 + index,
                ContributesToProgress = true
            });
        }

        await db.SaveChangesAsync();
        return projectId;
    }

    private async Task<AssignmentTaskSeed> SeedAssistantAssignmentTaskAsync()
    {
        var organizationId = await SeedOwnedOrganizationWithRulebookAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var skill = await db.OrganizationSkills
            .Where(item => item.OrganizationId == organizationId && item.IsActive)
            .OrderBy(item => item.Name)
            .FirstAsync();
        var contributor = new User
        {
            FullName = "Assignment Alternative",
            Email = $"{Guid.NewGuid():N}@assistant-assignment.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        };
        var project = new Project
        {
            OrganizationId = organizationId,
            Name = $"P12 Assignment Project {Guid.NewGuid():N}",
            Code = $"AS-{Guid.NewGuid():N}"[..12],
            OwnerId = _factory.TestUserId,
            Status = "Active"
        };
        var task = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = _factory.TestUserId,
            Title = "Implement verified booking flow",
            Description = "Build and verify the canonical booking flow.",
            Status = "Todo",
            Priority = "High",
            EstimatedHours = 8,
            DueDate = DateTimeOffset.UtcNow.Date.AddDays(10),
            RowVersion = [7, 8, 9]
        };
        var evidenceProject = new Project
        {
            OrganizationId = organizationId,
            Name = $"Alternative Evidence {Guid.NewGuid():N}",
            Code = $"AE-{Guid.NewGuid():N}"[..12],
            OwnerId = _factory.TestUserId,
            Status = "Archived"
        };
        var evidenceTask = new TaskItem
        {
            ProjectId = evidenceProject.Id,
            ReporterId = _factory.TestUserId,
            AssigneeId = contributor.Id,
            Title = "Confirmed alternative evidence",
            Status = "Done",
            Priority = "Medium",
            EstimatedHours = 6,
            ActualHours = 6,
            DueDate = DateTimeOffset.UtcNow.AddDays(-3)
        };
        db.AddRange(contributor, project, task, evidenceProject, evidenceTask);
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = contributor.Id,
            Role = OrganizationRoleRules.Member
        });
        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = contributor.Id,
            Role = ProjectRoleRules.Member
        });
        db.OrganizationMemberCapacityProfiles.Add(new OrganizationMemberCapacityProfile
        {
            OrganizationId = organizationId,
            UserId = contributor.Id,
            WeeklyCapacityHours = 32,
            TimeZoneId = "Asia/Ho_Chi_Minh"
        });
        db.TaskSkillRequirements.AddRange(
            new TaskSkillRequirement
            {
                TaskItemId = task.Id,
                OrganizationSkillId = skill.Id,
                RequiredLevel = "Intermediate",
                Provenance = "MANUAL",
                ConfirmedByUserId = _factory.TestUserId,
                ConfirmedAt = DateTimeOffset.UtcNow
            },
            new TaskSkillRequirement
            {
                TaskItemId = evidenceTask.Id,
                OrganizationSkillId = skill.Id,
                RequiredLevel = "Intermediate",
                Provenance = "MANUAL",
                ConfirmedByUserId = _factory.TestUserId,
                ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2)
            });
        db.TaskCompletionAttributions.Add(new TaskCompletionAttribution
        {
            TaskItemId = evidenceTask.Id,
            ContributorUserId = contributor.Id,
            ConfirmedByUserId = _factory.TestUserId,
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-3),
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Status = TaskCompletionAttribution.Confirmed
        });
        await db.SaveChangesAsync();
        return new AssignmentTaskSeed(project.Id, task.Id);
    }

    private async Task<Guid> SeedOwnedOrganizationWithRulebookAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var userId = _factory.TestUserId;
        if (!await db.Users.AnyAsync(item => item.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                FullName = "Assistant Launch Owner",
                Email = $"{userId:N}@assistant-launch.test",
                PasswordHash = "not-used",
                Role = "User",
                IsActive = true
            });
        }
        var organization = new Organization
        {
            Name = $"Launch Organization {Guid.NewGuid():N}",
            Code = $"LO-{Guid.NewGuid():N}"[..12],
            OwnerId = userId,
            IsActive = true
        };
        db.Organizations.Add(organization);
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = userId,
            Role = OrganizationRoleRules.Owner
        });
        db.OrganizationMemberCapacityProfiles.Add(new OrganizationMemberCapacityProfile
        {
            OrganizationId = organization.Id,
            UserId = userId,
            WeeklyCapacityHours = 40,
            TimeZoneId = "Asia/Ho_Chi_Minh"
        });
        var managerProfile = new ProfessionalProfileDefinition
        {
            OrganizationId = organization.Id,
            Key = "product-project-manager",
            Name = "Product / Project Manager",
            Category = "Product & Delivery",
            IsSystemSeed = true,
            IsActive = true
        };
        db.ProfessionalProfileDefinitions.Add(managerProfile);
        db.OrganizationMemberProfessionalProfiles.Add(new OrganizationMemberProfessionalProfile
        {
            OrganizationId = organization.Id,
            UserId = userId,
            ProfessionalProfileDefinitionId = managerProfile.Id,
            Proficiency = ProfessionalProfileCatalog.Expert,
            VerificationStatus = OrganizationMemberProfessionalProfile.Verified,
            Source = ProfessionalProfileCatalog.ManagerConfirmed,
            VerifiedByUserId = userId,
            VerifiedAt = DateTimeOffset.UtcNow.AddDays(-2),
            EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1)
        });
        var skillCatalog = new[]
        {
            new OrganizationSkill { OrganizationId = organization.Id, Name = "Frontend / Vue", NormalizedName = "frontend-vue", Category = "Chuyên môn", DefaultRequiredLevel = "Intermediate", IsSystemSeed = true, IsActive = true },
            new OrganizationSkill { OrganizationId = organization.Id, Name = "Backend / .NET", NormalizedName = "backend-dotnet", Category = "Chuyên môn", DefaultRequiredLevel = "Intermediate", IsSystemSeed = true, IsActive = true },
            new OrganizationSkill { OrganizationId = organization.Id, Name = "Security / Auth", NormalizedName = "security-auth", Category = "Chuyên môn", DefaultRequiredLevel = "Intermediate", IsSystemSeed = true, IsActive = true },
            new OrganizationSkill { OrganizationId = organization.Id, Name = "Database / EF Core & SQL", NormalizedName = "database-efcore-sql", Category = "Chuyên môn", DefaultRequiredLevel = "Intermediate", IsSystemSeed = true, IsActive = true },
            new OrganizationSkill { OrganizationId = organization.Id, Name = "QA / Playwright", NormalizedName = "qa-playwright", Category = "Chuyên môn", DefaultRequiredLevel = "Intermediate", IsSystemSeed = true, IsActive = true }
        };
        db.OrganizationSkills.AddRange(skillCatalog);
        var evidenceProject = new Project
        {
            OrganizationId = organization.Id,
            Name = "AI Skill Evidence Baseline",
            Code = $"EV-{Guid.NewGuid():N}"[..12],
            OwnerId = userId,
            Status = "Archived"
        };
        var evidenceTask = new TaskItem
        {
            ProjectId = evidenceProject.Id,
            ReporterId = userId,
            AssigneeId = userId,
            Title = "Confirmed delivery baseline",
            Description = "Evidence fixture for deterministic staffing tests.",
            Status = "Done",
            Priority = "Medium",
            EstimatedHours = 8,
            ActualHours = 8,
            DueDate = DateTimeOffset.UtcNow.AddDays(-2)
        };
        db.AddRange(evidenceProject, evidenceTask);
        db.TaskSkillRequirements.AddRange(skillCatalog.Select(skill => new TaskSkillRequirement
        {
            TaskItemId = evidenceTask.Id,
            OrganizationSkillId = skill.Id,
            RequiredLevel = "Intermediate",
            Provenance = "MANUAL",
            ConfirmedByUserId = userId,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2)
        }));
        db.TaskCompletionAttributions.Add(new TaskCompletionAttribution
        {
            TaskItemId = evidenceTask.Id,
            ContributorUserId = userId,
            ConfirmedByUserId = userId,
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-2),
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Status = TaskCompletionAttribution.Confirmed
        });
        db.OrganizationWorkRuleSets.Add(new OrganizationWorkRuleSet
        {
            OrganizationId = organization.Id,
            Version = 1,
            Status = "active",
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            RulesJson = JsonSerializer.Serialize(new[]
            {
                new OrganizationWorkRuleDto(
                    "active_membership_required", "governance", "block",
                    "Requester must be an active organization member.")
            }, JsonOptions),
            CreatedByUserId = userId,
            ActivatedByUserId = userId,
            ActivatedAt = DateTimeOffset.UtcNow,
            Revision = 2
        });
        await db.SaveChangesAsync();
        return organization.Id;
    }

    private async Task<Guid> SeedForeignProjectAsync(bool addCurrentUserAsViewer)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!await db.Users.AnyAsync(item => item.Id == _factory.TestUserId))
        {
            db.Users.Add(new User
            {
                Id = _factory.TestUserId,
                FullName = "Assistant Context Viewer",
                Email = $"{_factory.TestUserId:N}@assistant-context.test",
                PasswordHash = "not-used",
                Role = "User",
                IsActive = true
            });
        }
        var owner = new User
        {
            FullName = "Foreign Assistant Owner",
            Email = $"{Guid.NewGuid():N}@assistant-context.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        };
        var project = new Project
        {
            Name = $"Foreign Assistant Project {Guid.NewGuid():N}",
            Code = $"FX-{Guid.NewGuid():N}"[..12],
            OwnerId = owner.Id,
            Status = "Active"
        };
        db.AddRange(owner, project);
        if (addCurrentUserAsViewer)
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = _factory.TestUserId,
                Role = "Viewer"
            });
        }
        await db.SaveChangesAsync();
        return project.Id;
    }

    private sealed record AssignmentTaskSeed(Guid ProjectId, Guid TaskId);
    private sealed record CsrfResponse(string Token);
}
