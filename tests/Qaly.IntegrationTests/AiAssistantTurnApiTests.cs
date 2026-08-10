using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
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
        turn.Conversation.Guidance!.Steps.Should().OnlyContain(item =>
            new[] { "/projects", "/teams", "/tasks" }.Contains(item.Route, StringComparer.Ordinal));
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
        plan.DeliveryPlan.Sprints.SelectMany(item => item.Tasks).Should().HaveCount(2);
        plan.ExecutionReceipt.Should().BeNull();

        using (var beforeScope = _factory.Services.CreateScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            (await beforeDb.Projects.CountAsync(item => item.OrganizationId == organizationId)).Should().Be(0);
        }

        var scenarioId = plan.StaffingScenarios.Single(item => item.Feasible).ScenarioId;
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
            (await db.Projects.CountAsync(item => item.OrganizationId == organizationId)).Should().Be(1);
            (await db.ProjectMembers.CountAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId)).Should().Be(1);
            (await db.Set<Sprint>().CountAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId)).Should().Be(1);
            (await db.TaskItems.CountAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId)).Should().Be(2);
            (await db.TaskDependencies.CountAsync(item => item.Predecessor.ProjectId == confirmed.ExecutionReceipt.ProjectId)).Should().Be(1);
            var overdueTask = await db.TaskItems.FirstAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId);
            overdueTask.DueDate = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var monitored = await SendLaunchCommandAsync<MonitorProjectLaunchExecutionRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/executions/{confirmed.ExecutionReceipt.ReceiptId:D}/monitor",
            new MonitorProjectLaunchExecutionRequestDto(confirmed.ExecutionReceipt.Revision));
        monitored.LatestReplanProposal.Should().NotBeNull();
        monitored.LatestReplanProposal!.State.Should().Be("pending_review");
        monitored.LatestReplanProposal.Changes.Should().Contain(item => item.ChangeType == "tasks_overdue");
        monitored.LatestReplanProposal.RequiresConfirmation.Should().BeTrue();

        using (var monitorScope = _factory.Services.CreateScope())
        {
            var db = monitorScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var task = await db.TaskItems.FirstAsync(item => item.ProjectId == confirmed.ExecutionReceipt.ProjectId);
            task.DueDate.Should().BeBefore(DateTimeOffset.UtcNow);
            task.Status.Should().Be("Todo");
        }

        var rollback = await SendLaunchCommandAsync<RollbackProjectLaunchExecutionRequestDto, ProjectLaunchPlanDto>(
            $"/api/ai/project-launch/executions/{confirmed.ExecutionReceipt.ReceiptId:D}/rollback",
            new RollbackProjectLaunchExecutionRequestDto(true, monitored.ExecutionReceipt!.Revision, "Integration rollback before user work"),
            $"integration-rollback-{confirmed.ExecutionReceipt.ReceiptId:N}");
        rollback.State.Should().Be("rolled_back");
        rollback.ExecutionReceipt!.State.Should().Be("rolled_back");
        rollback.ExecutionReceipt.RollbackAvailable.Should().BeFalse();
        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var rolledBackProject = await finalDb.Projects.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == confirmed.ExecutionReceipt.ProjectId);
        rolledBackProject.IsDeleted.Should().BeTrue();
        (await finalDb.AuditLogs.CountAsync(item => item.EntityId == plan.PlanId.ToString() || item.EntityId == confirmed.ExecutionReceipt.ReceiptId.ToString()))
            .Should().BeGreaterThanOrEqualTo(2);
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

        var scenarioId = plan.StaffingScenarios.Single(item => item.Feasible).ScenarioId;
        var response = await SendLaunchCommandResponseAsync(
            $"/api/ai/project-launch/plans/{plan.PlanId:D}/confirm",
            new ConfirmProjectLaunchPlanRequestDto(true, plan.RowRevision, scenarioId),
            $"integration-stale-{plan.PlanId:N}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await afterDb.Projects.CountAsync(item => item.OrganizationId == organizationId)).Should().Be(0);
        (await afterDb.ProjectLaunchExecutions.CountAsync(item => item.ProjectLaunchPlanArtifactId == plan.PlanId)).Should().Be(0);
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
        turn.Answer!.Model!.Provider.Should().Be("DeepSeek");
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
        turn.ActualModel.Should().Be("deepseek-v4-pro");
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

        var inFlight = SendTurnAsync(request, $"assistant-cancel-{Guid.NewGuid():N}");
        AssistantTurn? running = null;
        for (var attempt = 0; attempt < 80 && running == null; attempt++)
        {
            await Task.Delay(50);
            using var scope = _factory.Services.CreateScope();
            running = await scope.ServiceProvider.GetRequiredService<QalyDbContext>().AssistantTurns
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.SessionId == session.SessionId && item.Status == "running");
        }
        running.Should().NotBeNull("the durable turn is written before the delayed analysis adapter runs");

        var csrf = (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        using var cancelRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/ai/assistant/turns/{running!.Id:D}/cancel")
        {
            Content = JsonContent.Create(new AiAssistantTurnControlRequestDto(1))
        };
        cancelRequest.Headers.Add("X-CSRF-TOKEN", csrf);
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
        resumeRequest.Headers.Add("X-CSRF-TOKEN", csrf);
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
            "Khởi chạy một dự án web SPA production-ready trong 8 tuần",
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
