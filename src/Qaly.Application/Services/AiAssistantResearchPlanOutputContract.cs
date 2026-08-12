using System.Text.Json;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public static class AiAssistantResearchPlanOutputContract
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> Severities =
        new HashSet<string>(["info", "low", "medium", "high", "critical"], StringComparer.Ordinal);

    public static bool TryValidateModel(
        string modelJson,
        string? validationContextJson,
        out string? error)
    {
        if (!TryReadContext(validationContextJson, out var context, out error) ||
            !TryReadPlan(modelJson, out var plan, out error))
        {
            return false;
        }

        return TryValidateAndCanonicalize(plan!, context!, "pending", "pending", out _, out error);
    }

    public static bool TryBuildResult(
        string modelJson,
        string validationContextJson,
        string actualProvider,
        string actualModel,
        out AiAssistantResearchPlanDto? result,
        out string? error)
    {
        result = null;
        if (!TryReadContext(validationContextJson, out var context, out error) ||
            !TryReadPlan(modelJson, out var plan, out error))
        {
            return false;
        }

        return TryValidateAndCanonicalize(
            plan!,
            context!,
            NormalizeText(actualProvider, 120) ?? "unknown",
            NormalizeText(actualModel, 160) ?? "unknown",
            out result,
            out error);
    }

    private static bool TryValidateAndCanonicalize(
        AiAssistantResearchPlanDto plan,
        AiAssistantResearchValidationContextDto context,
        string actualProvider,
        string actualModel,
        out AiAssistantResearchPlanDto? result,
        out string? error)
    {
        result = null;
        error = null;
        var allowedRefs = context.AllowedSourceRefs.ToHashSet(StringComparer.Ordinal);
        var authorizedCapabilities = context.AuthorizedCapabilityIds.ToHashSet(StringComparer.Ordinal);
        var objective = NormalizeText(plan.Objective, 2000);
        var authorizedObjective = NormalizeText(context.Objective, 2000);
        if (!string.Equals(plan.SchemaId, AiAssistantResearchPlanContract.SchemaId, StringComparison.Ordinal) ||
            objective == null || authorizedObjective == null || plan.Scope == null ||
            !string.Equals(plan.PromptId, AiAssistantResearchPlanContract.PromptId, StringComparison.Ordinal) ||
            !string.Equals(plan.PromptVersion, AiAssistantResearchPlanContract.PromptVersion, StringComparison.Ordinal))
        {
            error = "Research plan has an invalid schema, prompt version, objective, or scope.";
            return false;
        }

        var scopeLabel = NormalizeText(plan.Scope.Label, 300);
        var scopeType = NormalizeText(plan.Scope.ScopeType, 80);
        var scopeRefs = NormalizeRefs(plan.Scope.SourceRefs);
        if (scopeLabel == null || scopeType == null || plan.Scope.ProjectId != context.ProjectId ||
            scopeRefs.Count < 1 || scopeRefs.Any(reference => !allowedRefs.Contains(reference)))
        {
            error = "Research scope must match the authorized project and cite only authorized sources.";
            return false;
        }

        if (plan.Findings == null || plan.Findings.Count > 12 ||
            plan.Unknowns == null || plan.Unknowns.Count > 8 ||
            plan.Assumptions == null || plan.Assumptions.Count > 10 ||
            plan.Options == null || plan.Options.Count is < 1 or > 3 ||
            plan.ProposedActions == null || plan.ProposedActions.Count > 8 ||
            plan.Warnings == null || plan.Warnings.Count > 10)
        {
            error = "Research plan exceeds the bounded findings, unknowns, assumptions, options, actions, or warnings contract.";
            return false;
        }

        var findings = new List<AiAssistantResearchFindingDto>(plan.Findings.Count);
        foreach (var finding in plan.Findings)
        {
            var id = NormalizeText(finding.FindingId, 80);
            var statement = NormalizeText(finding.Statement, 1000);
            var severity = finding.Severity?.Trim().ToLowerInvariant();
            var refs = NormalizeRefs(finding.SourceRefs);
            if (id == null || statement == null || severity == null || !Severities.Contains(severity) ||
                finding.Confidence is < 0 or > 1 || refs.Count < 1 || refs.Any(reference => !allowedRefs.Contains(reference)))
            {
                error = "Every factual finding must be bounded, confidence-scored, and grounded in authorized source references.";
                return false;
            }

            findings.Add(new AiAssistantResearchFindingDto(
                id,
                statement,
                severity,
                Math.Round(finding.Confidence, 2, MidpointRounding.AwayFromZero),
                refs));
        }
        if (findings.Select(item => item.FindingId).Distinct(StringComparer.Ordinal).Count() != findings.Count)
        {
            error = "Research finding IDs must be unique.";
            return false;
        }

        var unknowns = new List<AiAssistantResearchUnknownDto>(plan.Unknowns.Count);
        foreach (var unknown in plan.Unknowns)
        {
            var id = NormalizeText(unknown.UnknownId, 80);
            var question = NormalizeText(unknown.Question, 500);
            if (id == null || question == null)
            {
                error = "Every unknown requires a bounded ID and clarification question.";
                return false;
            }
            unknowns.Add(new AiAssistantResearchUnknownDto(id, question, unknown.Blocking));
        }
        if (unknowns.Select(item => item.UnknownId).Distinct(StringComparer.Ordinal).Count() != unknowns.Count ||
            findings.Count == 0 && unknowns.Count == 0)
        {
            error = "Research plan must contain grounded findings or explicit unknowns, with unique IDs.";
            return false;
        }

        var options = new List<AiAssistantResearchOptionDto>(plan.Options.Count);
        foreach (var option in plan.Options)
        {
            var id = NormalizeText(option.OptionId, 80);
            var title = NormalizeText(option.Title, 200);
            var outcome = NormalizeText(option.Outcome, 800);
            var effort = NormalizeText(option.EstimatedEffort, 200);
            var risk = NormalizeText(option.Risk, 500);
            var tradeOffs = NormalizeList(option.TradeOffs, 8, 400);
            if (id == null || title == null || outcome == null || effort == null || risk == null || tradeOffs.Count < 1)
            {
                error = "Every option requires an ID, outcome, trade-offs, effort, and risk.";
                return false;
            }
            options.Add(new AiAssistantResearchOptionDto(id, title, outcome, tradeOffs, effort, risk));
        }
        if (options.Select(item => item.OptionId).Distinct(StringComparer.Ordinal).Count() != options.Count ||
            options.All(item => !string.Equals(item.OptionId, plan.RecommendedOptionId, StringComparison.Ordinal)))
        {
            error = "Option IDs must be unique and the recommendation must select one returned option.";
            return false;
        }

        var actionIds = plan.ProposedActions
            .Select(action => NormalizeText(action.ActionId, 80))
            .ToList();
        if (actionIds.Any(id => id == null) || actionIds.Distinct(StringComparer.Ordinal).Count() != actionIds.Count)
        {
            error = "Proposed action IDs must be present and unique.";
            return false;
        }

        var knownActionIds = actionIds.Cast<string>().ToHashSet(StringComparer.Ordinal);
        var actions = new List<AiAssistantResearchActionDto>(plan.ProposedActions.Count);
        foreach (var action in plan.ProposedActions)
        {
            var actionId = NormalizeText(action.ActionId, 80)!;
            var capabilityId = NormalizeText(action.CapabilityId, 120);
            var title = NormalizeText(action.Title, 300);
            var dependencies = NormalizeList(action.DependencyIds, 8, 80);
            var refs = NormalizeRefs(action.SourceRefs);
            if (capabilityId == null || title == null || dependencies.Any(id => !knownActionIds.Contains(id) || id == actionId) ||
                refs.Count < 1 || refs.Any(reference => !allowedRefs.Contains(reference)) ||
                action.DraftInput.ValueKind != JsonValueKind.Object || action.DraftInput.GetRawText().Length > 8000)
            {
                error = "Every proposed action must have a valid graph edge, object draft input, and authorized source references.";
                return false;
            }

            var executionEligible =
                string.Equals(capabilityId, AiAssistantContextContract.TaskCreateCapability, StringComparison.Ordinal) &&
                authorizedCapabilities.Contains(capabilityId) && context.ProjectId.HasValue;
            var eligibilityReason = executionEligible
                ? "registered_draft_adapter"
                : authorizedCapabilities.Contains(capabilityId)
                    ? "capability_not_executable_from_research_plan"
                    : "capability_not_registered_or_authorized";
            var draftInput = executionEligible
                ? JsonSerializer.SerializeToElement(new
                {
                    message = ReadOptionalString(action.DraftInput, "message") ?? context.Objective,
                    projectId = context.ProjectId,
                    schemaId = AiAssistantTurnContract.SchemaId,
                    intent = AiAssistantContextContract.TaskCreateCapability
                }, JsonOptions)
                : action.DraftInput.Clone();
            actions.Add(new AiAssistantResearchActionDto(
                actionId,
                capabilityId,
                title,
                dependencies,
                draftInput,
                refs,
                executionEligible,
                eligibilityReason));
        }
        if (HasDependencyCycle(actions))
        {
            error = "Proposed action graph contains a dependency cycle.";
            return false;
        }

        var rationale = NormalizeText(plan.RecommendationRationale, 1200);
        if (rationale == null)
        {
            error = "Research recommendation requires a bounded rationale.";
            return false;
        }

        result = new AiAssistantResearchPlanDto(
            AiAssistantResearchPlanContract.SchemaId,
            AiAssistantResearchPlanContract.PromptId,
            AiAssistantResearchPlanContract.PromptVersion,
            authorizedObjective,
            new AiAssistantResearchScopeDto(
                context.ProjectId.HasValue ? "project" : "workspace",
                context.ProjectId,
                context.ScopeLabel.Trim(),
                scopeRefs),
            findings,
            unknowns,
            NormalizeList(plan.Assumptions, 10, 500),
            options,
            plan.RecommendedOptionId,
            rationale,
            actions,
            NormalizeList(plan.Warnings, 10, 500),
            NormalizeList(context.PrivacyNotes, 12, 500),
            context.FreshnessAt,
            DateTimeOffset.UtcNow,
            actualProvider,
            actualModel);
        return true;
    }

    private static bool TryReadPlan(string json, out AiAssistantResearchPlanDto? plan, out string? error)
    {
        plan = null;
        try
        {
            plan = JsonSerializer.Deserialize<AiAssistantResearchPlanDto>(CleanJson(json), JsonOptions);
            error = plan == null ? "Research plan response is empty." : null;
            return plan != null;
        }
        catch (JsonException exception)
        {
            error = $"Research plan response is invalid JSON: {exception.Message}";
            return false;
        }
    }

    private static bool TryReadContext(
        string? json,
        out AiAssistantResearchValidationContextDto? context,
        out string? error)
    {
        context = null;
        try
        {
            context = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<AiAssistantResearchValidationContextDto>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Research validation context is invalid JSON: {exception.Message}";
            return false;
        }

        if (context == null || NormalizeText(context.Objective, 2000) == null ||
            NormalizeText(context.ScopeLabel, 300) == null || context.AllowedSourceRefs == null ||
            context.AllowedSourceRefs.Count < 1 || context.AllowedSourceRefs.Any(string.IsNullOrWhiteSpace) ||
            context.AuthorizedCapabilityIds == null || context.PrivacyNotes == null || context.FreshnessAt == default)
        {
            error = "Research validation context is missing its objective, scope, or authorized source references.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool HasDependencyCycle(IReadOnlyList<AiAssistantResearchActionDto> actions)
    {
        var dependencies = actions.ToDictionary(item => item.ActionId, item => item.DependencyIds, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (visiting.Contains(id)) return true;
            if (!visited.Add(id)) return false;
            visiting.Add(id);
            foreach (var dependency in dependencies[id])
            {
                if (Visit(dependency)) return true;
            }
            visiting.Remove(id);
            return false;
        }
        return actions.Any(action => Visit(action.ActionId));
    }

    private static string CleanJson(string value)
    {
        var cleaned = value.Trim();
        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[7..];
        if (cleaned.EndsWith("```", StringComparison.Ordinal)) cleaned = cleaned[..^3];
        return cleaned.Trim();
    }

    private static List<string> NormalizeRefs(IReadOnlyList<string>? values)
        => NormalizeList(values, 16, 500);

    private static List<string> NormalizeList(IReadOnlyList<string>? values, int maxCount, int maxLength)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => value.Length <= maxLength)
            .Distinct(StringComparer.Ordinal)
            .Take(maxCount)
            .ToList();

    private static string? NormalizeText(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength ? null : normalized;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? NormalizeText(value.GetString(), 2000)
            : null;
}
