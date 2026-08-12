using System.Text.Json;
using FluentAssertions;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.UnitTests;

public sealed class AiAssistantQualityEvaluatorTests
{
    [Fact]
    public void VersionedVietnameseEvalSet_EnforcesUsefulAndDeadEndContracts()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-native-conversation-quality.vi-v1.json");
        var cases = JsonSerializer.Deserialize<List<EvalCase>>(File.ReadAllText(file))!;
        cases.Should().HaveCountGreaterThanOrEqualTo(6);

        foreach (var item in cases)
        {
            var response = item.Expect == "dead_end"
                ? Response("Chưa có skill để làm việc này.", null)
                : Response(
                    "Mình đã hiểu mục tiêu. Đây là phương án tạm thời có dữ liệu cần xác nhận và bước tiếp theo rõ ràng để bạn tiếp tục an toàn.",
                    item.Expect == "policy" ? null : AiAssistantManualGuidanceRegistry.ForCapability("project.create.v1"),
                    item.Expect == "policy" ? "policy_blocked" : "guided_answer");
            var evaluation = AiAssistantQualityEvaluator.Evaluate(response);
            if (item.Expect == "dead_end") evaluation.DeadEnd.Should().BeTrue(item.Id);
            else evaluation.DeadEnd.Should().BeFalse(item.Id);
        }
    }

    [Fact]
    public void FalseProjectCreationClaim_WithoutReceipt_IsRejected()
    {
        var response = Response("Dự án đã tạo project thành công.", null, "guided_answer") with
        {
            Intent = AiProjectOrchestrationContract.ExecuteCapabilityId,
            ExecutionPolicy = "explicit_batch_confirm"
        };

        var evaluation = AiAssistantQualityEvaluator.Evaluate(response);

        evaluation.FalseMutationSuccess.Should().BeTrue();
        evaluation.Passed.Should().BeFalse();
    }

    private static AiAssistantTurnResponseDto Response(
        string answer,
        AiAssistantManualGuidanceDto? guidance,
        string disposition = "guided_answer")
        => new(
            AiAssistantTurnContract.SchemaId,
            disposition,
            AiAssistantTurnContract.GuidedAnswerIntent,
            "read_only",
            answer,
            0.8,
            null,
            null,
            [],
            ActualProvider: "Qaly Local Guidance",
            ActualModel: "deterministic-failsoft-v1",
            Conversation: new AiAssistantConversationTurnDto(
                AiAssistantConversationContract.SchemaId,
                "guided",
                "available",
                answer,
                [],
                guidance,
                null,
                [],
                [],
                0.8,
                "Qaly Local Guidance",
                "deterministic-failsoft-v1"));

    private sealed record EvalCase(string Id, string Prompt, string Expect);
}
