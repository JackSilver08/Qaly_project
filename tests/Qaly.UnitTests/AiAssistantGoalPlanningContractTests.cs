using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Interfaces;

#pragma warning disable CA1861

namespace Qaly.UnitTests;

public sealed class AiAssistantGoalPlanningContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BuildResult_ReconcilesModelScopeAndSelectsOnlyAuthorizedRegisteredSkill()
    {
        var projectId = Guid.NewGuid();
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.ResearchPlanCapability, out var research).Should().BeTrue();
        var context = new AiAssistantGoalPlanningValidationContextDto(
            "Phân tích và đề xuất phương án",
            new AiAssistantClientContextDto("/projects", "project", projectId),
            null,
            [research!]);

        var ok = AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson("Phân tích dự án", research!.CapabilityId, modelProjectId: Guid.NewGuid()),
            JsonSerializer.Serialize(context, JsonOptions),
            "DeepSeek", "deepseek-v4-pro",
            out var result, out var error);

        ok.Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().Be(research.CapabilityId);
        result.GoalAnalysis.Scopes.Single().ProjectId.Should().Be(projectId);
        result.GoalAnalysis.ActualProvider.Should().Be("DeepSeek");
        result.WorkPlan.Steps.Should().ContainSingle(step => step.Kind == "call_skill");
    }

    [Fact]
    public void BuildResult_ModelInventedSkill_IsReportedButNeverSelected()
    {
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.GroundedReadCapability, out var read).Should().BeTrue();
        var context = new AiAssistantGoalPlanningValidationContextDto(
            "Chạy test demo tất cả CAND", new AiAssistantClientContextDto("/dashboard", "workspace"), null, [read!]);

        var ok = AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson("Chạy test demo", "shell.execute.v1", missingSkillId: "demo.test.run.v1"),
            JsonSerializer.Serialize(context, JsonOptions), "DeepSeek", "deepseek-v4-pro",
            out var result, out var error);

        ok.Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().BeNull();
        result.GoalAnalysis.Disposition.Should().Be("unsupported_but_analyzed");
        result.GoalAnalysis.MissingSkills.Should().Contain(item => item.SkillId == "demo.test.run.v1");
        result.WorkPlan.Steps.Should().NotContain(step => step.Kind == "call_skill");
    }

    [Fact]
    public void DeterministicFallback_DemoRequest_IsHonestAndNeverExecutes()
    {
        var context = new AiAssistantExecutionContextDto(WithoutSafeTestCapability(), [], []);
        var result = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            new AiAssistantTurnRequestDto("Chạy test demo tất cả CAND đã implement"), context, "provider_unavailable");

        result.UsedFallback.Should().BeTrue();
        result.SelectedCapabilityId.Should().BeNull();
        result.GoalAnalysis.ActualProvider.Should().Be("not_reached");
        result.GoalAnalysis.MissingSkills.Should().Contain(item => item.SkillId == "demo.test.run.v1");
    }

    [Fact]
    public void BuildResult_ModelSelectsReadSkillForDemoExecution_ServerVetoesHandoff()
    {
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.GroundedReadCapability, out var read).Should().BeTrue();
        var context = new AiAssistantGoalPlanningValidationContextDto(
            "chạy tự động để test các CAND đã implement",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            null,
            [read!]);

        var ok = AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson("Test implemented candidates", read!.CapabilityId),
            JsonSerializer.Serialize(context, JsonOptions),
            "DeepSeek", "deepseek-v4-pro",
            out var result, out var error);

        ok.Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().BeNull();
        result.GoalAnalysis.Disposition.Should().Be("unsupported_but_analyzed");
        result.GoalAnalysis.MissingSkills.Should().ContainSingle(item => item.SkillId == "demo.test.run.v1");
        result.WorkPlan.Steps.Should().NotContain(step => step.Kind == "call_skill");
    }

    [Fact]
    public void DeterministicFallback_NaturalProjectPhrase_SelectsArtifactOnlyProjectLaunchSkill()
    {
        var context = new AiAssistantExecutionContextDto(WithoutSafeTestCapability(), [], []);

        var result = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            new AiAssistantTurnRequestDto(
                "Tạo một dự án web SPA",
                new AiAssistantClientContextDto("/dashboard", "projects")),
            context,
            "goal_provider_unavailable");

        result.SelectedCapabilityId.Should().Be(AiProjectLaunchContract.CapabilityId);
        result.GoalAnalysis.Disposition.Should().Be("plannable");
        result.GoalAnalysis.MissingSkills.Should().BeEmpty();
        result.WorkPlan.Steps.Should().Contain(step =>
            step.Kind == "call_skill" && step.SkillId == AiProjectLaunchContract.CapabilityId && step.MutationClass == "none");
    }

    [Fact]
    public async Task Planner_KnownDemoExecutionRequest_DoesNotCallProvider()
    {
        var gateway = new Mock<IAiGateway>(MockBehavior.Strict);
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        var planner = new AiAssistantGoalPlanner(
            gateway.Object,
            currentUser.Object,
            Options.Create(new AiJobPlatformOptions { AssistantGoalPlannerEnabled = true }));
        var context = new AiAssistantExecutionContextDto(WithoutSafeTestCapability(), [], []);

        var result = await planner.PlanAsync(
            new AiAssistantTurnRequestDto(
                "chạy tự động để test các CAND đã implement",
                new AiAssistantClientContextDto("/dashboard", "workspace")),
            context);

        result.IsSuccess.Should().BeTrue();
        result.Data!.SelectedCapabilityId.Should().BeNull();
        result.Data.GoalAnalysis.ActualProvider.Should().Be("Qaly policy router");
        gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public void DeterministicFallback_DemoRequest_WithDevCapability_PreparesConfirmedAdapterHandoff()
    {
        var context = new AiAssistantExecutionContextDto(AiAssistantCapabilityCatalog.All.ToArray(), [], []);

        var result = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            new AiAssistantTurnRequestDto("Chạy test demo tất cả CAND đã implement"), context, "provider_unavailable");

        result.SelectedCapabilityId.Should().Be(AiSafeTestOrchestratorContract.CapabilityId);
        result.GoalAnalysis.MissingSkills.Should().BeEmpty();
        result.GoalAnalysis.RequiresConfirmation.Should().BeTrue();
        result.WorkPlan.Steps.Should().Contain(step =>
            step.Kind == "call_skill" && step.SkillId == AiSafeTestOrchestratorContract.CapabilityId);
    }

    [Fact]
    public void ValidateModel_RejectsCyclicWorkPlan()
    {
        var valid = AiAssistantGoalPlanningOutputContract.TryValidateModel(
            ModelJson("Analyze", AiAssistantContextContract.GroundedReadCapability, selfCycle: true),
            null,
            out var error);

        valid.Should().BeFalse();
        error.Should().Contain("cycle");
    }

    private static AiAssistantCapabilityDescriptorDto[] WithoutSafeTestCapability()
        => AiAssistantCapabilityCatalog.All
            .Where(item => item.CapabilityId != AiSafeTestOrchestratorContract.CapabilityId)
            .ToArray();

    private static string ModelJson(
        string objective,
        string rankedSkillId,
        Guid? modelProjectId = null,
        string? missingSkillId = null,
        bool selfCycle = false)
    {
        var scope = new
        {
            scopeType = "project", projectId = modelProjectId, entityType = (string?)null, entityId = (Guid?)null,
            label = "Model scope", confidence = 0.8, reason = "Model guess"
        };
        return JsonSerializer.Serialize(new
        {
            schemaId = AiAssistantGoalPlanningContract.SchemaId,
            promptId = AiAssistantGoalPlanningContract.PromptId,
            promptVersion = AiAssistantGoalPlanningContract.PromptVersion,
            objective,
            userJob = objective,
            intentFacets = new[] { "analysis" },
            scopes = new[] { scope },
            constraints = Array.Empty<string>(),
            unknowns = Array.Empty<object>(),
            assumptions = Array.Empty<string>(),
            rankedSkills = new[] { new { skillId = rankedSkillId, fitReason = "Best fit", confidence = 0.9 } },
            missingSkills = missingSkillId == null
                ? Array.Empty<object>()
                : new object[] { new { skillId = missingSkillId, title = "Missing skill", reason = "Not registered", suggestedPath = "Add an adapter" } },
            riskLevel = "low",
            requiresConfirmation = false,
            disposition = missingSkillId == null ? "plannable" : "unsupported_but_analyzed",
            confidence = 0.9,
            warnings = Array.Empty<string>(),
            workPlan = new
            {
                schemaId = AiAssistantGoalPlanningContract.WorkPlanSchemaId,
                objective,
                scope,
                selectedSkillIds = new[] { rankedSkillId },
                steps = new[]
                {
                    new { stepId = "S1", kind = "analyze", publicLabel = "Analyze", skillId = (string?)null, sourceIds = Array.Empty<string>(), dependencyIds = selfCycle ? new[] { "S1" } : Array.Empty<string>(), expectedOutputSchemaId = (string?)null, verificationIds = new[] { "contract" }, mutationClass = "none", state = "planned" }
                },
                blockingUnknowns = Array.Empty<object>(),
                maxSteps = 8,
                maxAttemptsPerStep = 2,
                stopConditions = new[] { "policy_denied" },
                requiresPlanApproval = false
            }
        }, JsonOptions);
    }
}
