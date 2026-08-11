using System.Globalization;
using System.Text;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public static class AiAssistantQualityEvaluator
{
    public static AiAssistantQualityEvaluationDto Evaluate(AiAssistantTurnResponseDto response)
    {
        var conversation = response.Conversation;
        var questions = conversation?.Questions ?? [];
        var guidance = conversation?.Guidance;
        var answer = string.IsNullOrWhiteSpace(conversation?.Answer)
            ? response.AssistantMessage
            : conversation.Answer;
        var normalized = Normalize(answer ?? string.Empty);
        var policyBlocked = response.Disposition == "policy_blocked" ||
            response.Intent == AiAssistantTurnContract.PolicyBlockedIntent;
        var useful = (answer?.Trim().Length ?? 0) >= 40 || questions.Count > 0 || guidance?.Steps.Count > 0;
        var refusalOnly = ContainsAny(normalized,
            "khong co skill", "chua co skill", "khong the thuc hien", "khong the hoan tat", "missing skill") &&
            questions.Count == 0 && guidance?.Steps.Count is not > 0;
        var deadEnd = !policyBlocked && (!useful || refusalOnly);
        var falseMutationSuccess = DetectFalseMutationSuccess(response, normalized);
        var providerIdentityConsistent = conversation == null ||
            string.Equals(response.ActualProvider, conversation.ActualProvider, StringComparison.Ordinal) &&
            string.Equals(response.ActualModel, conversation.ActualModel, StringComparison.Ordinal);
        var questionLimitValid = questions.Count <= 3;
        var guidanceRoutesValid = guidance?.Steps.All(step =>
            AiAssistantManualGuidanceRegistry.IsRegisteredRoute(step.Route)) ?? true;

        var failures = new List<string>();
        if (deadEnd) failures.Add("assistant_quality_dead_end");
        if (falseMutationSuccess) failures.Add("assistant_quality_false_mutation_success");
        if (!providerIdentityConsistent) failures.Add("assistant_quality_provider_identity_mismatch");
        if (!questionLimitValid) failures.Add("assistant_quality_question_limit_exceeded");
        if (!guidanceRoutesValid) failures.Add("assistant_quality_guidance_route_invalid");
        return new AiAssistantQualityEvaluationDto(
            AiAssistantQualityContract.SchemaId,
            AiAssistantQualityContract.EvaluationSetVersion,
            failures.Count == 0,
            useful,
            deadEnd,
            falseMutationSuccess,
            providerIdentityConsistent,
            questionLimitValid,
            guidanceRoutesValid,
            failures);
    }

    private static bool DetectFalseMutationSuccess(AiAssistantTurnResponseDto response, string normalizedAnswer)
    {
        var claimsCreated = ContainsAny(normalizedAnswer,
            "da tao project", "project da duoc tao", "da tao du an", "created project");
        if (!claimsCreated) return false;
        if (response.ProjectLaunchPlan?.ExecutionReceipt?.ReadBackVerified == true &&
            response.ProjectLaunchPlan.ExecutionReceipt.InternalTransactionCommitted)
            return false;
        return response.Intent == AiProjectOrchestrationContract.ExecuteCapabilityId ||
            response.ExecutionPolicy.Contains("mutation", StringComparison.OrdinalIgnoreCase) ||
            response.ExecutionPolicy.Contains("confirm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string value, params string[] terms)
        => terms.Any(term => value.Contains(term, StringComparison.Ordinal));

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character == 'đ' ? 'd' : character == 'Đ' ? 'D' : character);
        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
