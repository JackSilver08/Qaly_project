using System.Text.Json;

namespace Qaly.Application.DTOs.Ai;

public static class AiAssistantResearchPlanContract
{
    public const string CapabilityId = "research.plan.v1";
    public const string SchemaId = "assistant_research_plan.v1";
    public const string RequestSchemaId = "assistant_research_request.v1";
    public const string RendererId = "research-plan-review.v1";
    public const string PromptId = "assistant-research-plan";
    public const string PromptVersion = "1.0.0";
}

public sealed record AiAssistantResearchScopeDto(
    string ScopeType,
    Guid? ProjectId,
    string Label,
    IReadOnlyList<string> SourceRefs);

public sealed record AiAssistantResearchFindingDto(
    string FindingId,
    string Statement,
    string Severity,
    double Confidence,
    IReadOnlyList<string> SourceRefs);

public sealed record AiAssistantResearchUnknownDto(
    string UnknownId,
    string Question,
    bool Blocking);

public sealed record AiAssistantResearchOptionDto(
    string OptionId,
    string Title,
    string Outcome,
    IReadOnlyList<string> TradeOffs,
    string EstimatedEffort,
    string Risk);

public sealed record AiAssistantResearchActionDto(
    string ActionId,
    string CapabilityId,
    string Title,
    IReadOnlyList<string> DependencyIds,
    JsonElement DraftInput,
    IReadOnlyList<string> SourceRefs,
    bool ExecutionEligible,
    string EligibilityReason);

public sealed record AiAssistantResearchPlanDto(
    string SchemaId,
    string PromptId,
    string PromptVersion,
    string Objective,
    AiAssistantResearchScopeDto Scope,
    IReadOnlyList<AiAssistantResearchFindingDto> Findings,
    IReadOnlyList<AiAssistantResearchUnknownDto> Unknowns,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<AiAssistantResearchOptionDto> Options,
    string RecommendedOptionId,
    string RecommendationRationale,
    IReadOnlyList<AiAssistantResearchActionDto> ProposedActions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> PrivacyNotes,
    DateTimeOffset FreshnessAt,
    DateTimeOffset GeneratedAt,
    string ActualProvider,
    string ActualModel);

public sealed record AiAssistantResearchValidationContextDto(
    string Objective,
    Guid? ProjectId,
    string ScopeLabel,
    DateTimeOffset FreshnessAt,
    IReadOnlyList<string> PrivacyNotes,
    IReadOnlyList<string> AllowedSourceRefs,
    IReadOnlyList<string> AuthorizedCapabilityIds);
