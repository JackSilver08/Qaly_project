using System.Text.Json;
using FluentAssertions;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiAssistantResearchPlanContractTests
{
    private const string SourceRef = "qaly://project/11111111-1111-1111-1111-111111111111/summary@abc123";
    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void TryBuildResult_ReconcilesRegisteredAndUnknownActionsServerSide()
    {
        var context = ContextJson(includeTaskCreate: true);
        var model = ValidPlanJson([
            new
            {
                actionId = "A1",
                capabilityId = "task.create.v1",
                title = "Soạn task xử lý rủi ro",
                dependencyIds = Array.Empty<string>(),
                draftInput = new { message = "Tạo task xử lý rủi ro đăng nhập" },
                sourceRefs = new[] { SourceRef },
                executionEligible = false,
                eligibilityReason = "model_claim"
            },
            new
            {
                actionId = "A2",
                capabilityId = "project.create.v1",
                title = "Tạo project mới",
                dependencyIds = new[] { "A1" },
                draftInput = new { name = "Untrusted project" },
                sourceRefs = new[] { SourceRef },
                executionEligible = true,
                eligibilityReason = "model_claim"
            }
        ]);

        var success = AiAssistantResearchPlanOutputContract.TryBuildResult(
            model,
            context,
            "DeepSeek",
            "deepseek-reasoner",
            out var result,
            out var error);

        success.Should().BeTrue(error);
        result!.ActualProvider.Should().Be("DeepSeek");
        result.ActualModel.Should().Be("deepseek-reasoner");
        result.ProposedActions.Single(item => item.ActionId == "A1").ExecutionEligible.Should().BeTrue();
        result.ProposedActions.Single(item => item.ActionId == "A1").EligibilityReason.Should().Be("registered_draft_adapter");
        result.ProposedActions.Single(item => item.ActionId == "A2").ExecutionEligible.Should().BeFalse();
        result.ProposedActions.Single(item => item.ActionId == "A2").EligibilityReason.Should().Be("capability_not_registered_or_authorized");
    }

    [Fact]
    public void TryValidateModel_RejectsHallucinatedSourceReference()
    {
        var json = ValidPlanJson([]).Replace(SourceRef, "qaly://foreign/private@secret", StringComparison.Ordinal);

        var success = AiAssistantResearchPlanOutputContract.TryValidateModel(
            json,
            ContextJson(includeTaskCreate: false),
            out var error);

        success.Should().BeFalse();
        error.Should().Contain("authorized source", Exactly.Once());
    }

    [Fact]
    public void TryValidateModel_RejectsCyclicActionGraph()
    {
        var json = ValidPlanJson([
            Action("A1", ["A2"]),
            Action("A2", ["A1"])
        ]);

        var success = AiAssistantResearchPlanOutputContract.TryValidateModel(
            json,
            ContextJson(includeTaskCreate: false),
            out var error);

        success.Should().BeFalse();
        error.Should().Contain("cycle");
    }

    [Fact]
    public void AiOutputValidator_RoutesResearchSchemaThroughStrictContract()
    {
        var validator = new AiOutputValidator();

        validator.Validate(
            ValidPlanJson([]),
            AiAssistantResearchPlanContract.SchemaId,
            ContextJson(includeTaskCreate: false),
            out var validError).Should().BeTrue(validError);
        validator.Validate(
            "{\"schemaId\":\"assistant_research_plan.v1\"}",
            AiAssistantResearchPlanContract.SchemaId,
            ContextJson(includeTaskCreate: false),
            out var invalidError).Should().BeFalse();
        invalidError.Should().NotBeNullOrWhiteSpace();
    }

    private static object Action(string id, string[] dependencies)
        => new
        {
            actionId = id,
            capabilityId = "unknown.proposal.v1",
            title = $"Proposal {id}",
            dependencyIds = dependencies,
            draftInput = new { note = id },
            sourceRefs = new[] { SourceRef },
            executionEligible = true,
            eligibilityReason = "model_claim"
        };

    private static string ContextJson(bool includeTaskCreate)
        => JsonSerializer.Serialize(new AiAssistantResearchValidationContextDto(
            "Đánh giá rủi ro và đề xuất phương án",
            ProjectId,
            "Qaly Native AI",
            DateTimeOffset.UtcNow,
            ["project_private được kiểm quyền"],
            [SourceRef],
            includeTaskCreate ? ["research.plan.v1", "task.create.v1"] : ["research.plan.v1"]));

    private static string ValidPlanJson(object[] actions)
        => JsonSerializer.Serialize(new
        {
            schemaId = AiAssistantResearchPlanContract.SchemaId,
            promptId = AiAssistantResearchPlanContract.PromptId,
            promptVersion = AiAssistantResearchPlanContract.PromptVersion,
            objective = "Đánh giá rủi ro và đề xuất phương án",
            scope = new
            {
                scopeType = "project",
                projectId = ProjectId,
                label = "Qaly Native AI",
                sourceRefs = new[] { SourceRef }
            },
            findings = new[]
            {
                new
                {
                    findingId = "F1",
                    statement = "Dự án có một task quá hạn cần được ưu tiên.",
                    severity = "high",
                    confidence = 0.91,
                    sourceRefs = new[] { SourceRef }
                }
            },
            unknowns = new[] { new { unknownId = "U1", question = "Capacity tuần tới là bao nhiêu?", blocking = false } },
            assumptions = new[] { "Capacity chưa được xác minh." },
            options = new[]
            {
                new
                {
                    optionId = "O1",
                    title = "Xử lý rủi ro trước",
                    outcome = "Giảm rủi ro deadline.",
                    tradeOffs = new[] { "Lùi hạng mục ít quan trọng." },
                    estimatedEffort = "1-2 ngày",
                    risk = "Có thể chậm hạng mục phụ."
                }
            },
            recommendedOptionId = "O1",
            recommendationRationale = "Đây là phương án bám sát fact có nguồn.",
            proposedActions = actions,
            warnings = new[] { "Cần xác minh capacity trước khi giao việc." },
            privacyNotes = new[] { "model_claim" },
            freshnessAt = DateTimeOffset.UnixEpoch,
            generatedAt = DateTimeOffset.UnixEpoch,
            actualProvider = "model_claim",
            actualModel = "model_claim"
        });
}
