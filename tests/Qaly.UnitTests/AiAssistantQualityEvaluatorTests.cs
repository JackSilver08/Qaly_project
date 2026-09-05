using System.Text.Json;
using FluentAssertions;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.UnitTests;

public sealed class AiAssistantQualityEvaluatorTests
{
    [Fact]
    public void VietnameseRoutingEvalV2_CoversRequiredCategories_AndMeetsAccuracyGate()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-native-routing.vi-v2.json");
        var cases = JsonSerializer.Deserialize<List<RoutingEvalCase>>(
            File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var requiredCategories = new[]
        {
            "intent_routing", "task_project_disambiguation", "exact_requested_count", "follow_up_context",
            "provider_fallback", "permission_denial", "mutation_confirmation", "no_repeat_questions"
        };

        cases.Select(item => item.Category).Should().Contain(requiredCategories);
        var routingCases = cases.Where(item => !string.IsNullOrWhiteSpace(item.ExpectedCapability)).ToList();
        routingCases.Should().NotBeEmpty();

        var routed = routingCases.Select(item =>
        {
            var history = new List<AiChatMessageDto>();
            if (!string.IsNullOrWhiteSpace(item.HistoryUser)) history.Add(new("user", item.HistoryUser));
            if (!string.IsNullOrWhiteSpace(item.HistoryAssistant)) history.Add(new("assistant", item.HistoryAssistant));
            var actual = AiAssistantCapabilityIntentClassifier.Infer(item.Prompt, history);
            return new { Case = item, Actual = actual };
        }).ToList();
        var mismatches = routed.Where(item =>
            !string.Equals(item.Actual, item.Case.ExpectedCapability, StringComparison.Ordinal)).ToList();

        mismatches.Should().BeEmpty("every regression fixture must pass, not hide behind the aggregate threshold");
        foreach (var item in routingCases.Where(item => item.ExpectedCount.HasValue &&
                     item.ExpectedCapability == AiAssistantContextContract.TaskCreateCapability))
            AiActionComposerService.ExtractRequestedTaskCount(item.Prompt).Should().Be(item.ExpectedCount, item.Id);

        ((double)(routingCases.Count - mismatches.Count) / routingCases.Count).Should().BeGreaterThanOrEqualTo(
            AiAssistantQualityContract.MinimumRoutingAccuracy,
            string.Join(", ", mismatches.Select(item =>
                $"{item.Case.Id}: expected {item.Case.ExpectedCapability}, actual {item.Actual}")));
        AiAssistantQualityContract.MaximumDeadEndRate.Should().BeLessThanOrEqualTo(0.01);
        AiAssistantQualityContract.MinimumFallbackQualityPassRate.Should().BeGreaterThanOrEqualTo(0.90);
        AiAssistantQualityContract.MaximumInteractiveLatencyMs.Should().Be(10_000);
    }

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

    private sealed record RoutingEvalCase(
        string Id,
        string Category,
        string Prompt,
        string? ExpectedCapability = null,
        int? ExpectedCount = null,
        string? HistoryUser = null,
        string? HistoryAssistant = null,
        string? ExpectedBehavior = null);
}
