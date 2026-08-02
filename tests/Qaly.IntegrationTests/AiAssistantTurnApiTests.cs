using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Ai;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiAssistantTurnApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AiAssistantTurnApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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
    public async Task UnsupportedMutation_ReturnsHonestDispositionWithoutJobOrArtifact()
    {
        int jobCountBefore;
        using (var beforeScope = _factory.Services.CreateScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            jobCountBefore = await beforeDb.AiJobs.CountAsync();
        }

        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Tạo một dự án mới tên Alpha",
            new AiAssistantClientContextDto("/dashboard", "projects")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Disposition.Should().Be("unsupported_but_analyzed");
        turn.Intent.Should().Be(AiAssistantTurnContract.UnsupportedIntent);
        turn.ExecutionPolicy.Should().Be("analyze_only");
        turn.Artifact.Should().BeNull();
        turn.GoalAnalysis.Should().NotBeNull();
        turn.GoalAnalysis!.MissingSkills.Should().Contain(item => item.SkillId == "project.create.v1");
        turn.WorkPlan.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.AiJobs.CountAsync()).Should().Be(jobCountBefore);
    }

    [Fact]
    [Trait("TestId", "TEST-GS-01")]
    public async Task BroadDemoRequest_IsUnderstoodAndReportsMissingSkillWithoutExecution()
    {
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Bạn hãy tự phân tích rồi chạy test demo tất cả CAND đã implement",
            new AiAssistantClientContextDto("/dashboard", "workspace")));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn!.Disposition.Should().Be("unsupported_but_analyzed");
        turn.ExecutionPolicy.Should().Be("analyze_only");
        turn.Artifact.Should().BeNull();
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
        turn!.Disposition.Should().Be("unsupported_but_analyzed");
        turn.ExecutionPolicy.Should().Be("analyze_only");
        turn.Artifact.Should().BeNull();
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
    public async Task TaskCreate_ForReadOnlyProjectMember_IsPolicyBlockedBeforeProviderOrArtifact()
    {
        var projectId = await SeedForeignProjectAsync(addCurrentUserAsViewer: true);
        var response = await PostTurnAsync(new AiAssistantTurnRequestDto(
            "Tạo task backend cho dự án",
            new AiAssistantClientContextDto("/dashboard", "project_tasks", projectId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var turn = await response.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions);
        turn.Should().NotBeNull();
        turn!.Disposition.Should().Be("policy_blocked");
        turn.Artifact.Should().BeNull();
        turn.ActualProvider.Should().Be("IntegrationProvider");
        turn.Capabilities.Should().NotContain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability);
        turn.SourceDisclosures.Should().Contain(item =>
            item.Status == "denied" && item.ReasonCode == "capability_not_authorized");
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

    private async Task<Guid> SeedForeignProjectAsync(bool addCurrentUserAsViewer)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
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

    private sealed record CsrfResponse(string Token);
}
