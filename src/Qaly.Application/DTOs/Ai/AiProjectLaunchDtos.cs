namespace Qaly.Application.DTOs.Ai;

public static class AiProjectLaunchContract
{
    public const string CapabilityId = "project.launch.analyze.v1";
    public const string RequestSchemaId = "project_launch_request.v1";
    public const string BriefSchemaId = "project_launch_brief.v1";
    public const string RuleDecisionSchemaId = "organization_work_rule_decision.v1";
    public const string RendererId = "project-launch-brief.v1";
    public const string PromptVersion = "project_launch_brief@1.0.0";
}

public sealed record OrganizationWorkRuleDto(
    string RuleKey,
    string Category,
    string Enforcement,
    string Description,
    decimal? NumericValue = null,
    string? Unit = null,
    IReadOnlyList<string>? Values = null,
    bool Enabled = true);

public sealed record OrganizationWorkRuleSetDto(
    Guid RuleSetId,
    Guid OrganizationId,
    int Version,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    IReadOnlyList<OrganizationWorkRuleDto> Rules,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    Guid? ActivatedByUserId,
    long Revision);

public sealed record CreateOrganizationWorkRuleSetRequestDto(
    IReadOnlyList<OrganizationWorkRuleDto> Rules,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveUntil = null);

public sealed record ActivateOrganizationWorkRuleSetRequestDto(long Revision);

public sealed record OrganizationWorkRuleDecisionDto(
    string SchemaId,
    Guid? RuleSetId,
    int? RuleSetVersion,
    string RuleKey,
    string Result,
    string Severity,
    string Explanation,
    IReadOnlyDictionary<string, object?> DeterministicFacts,
    bool ExceptionEligible,
    string SourceFreshness);

public sealed record ProjectLaunchBriefDto(
    Guid BriefId,
    string SchemaId,
    int Revision,
    string State,
    Guid OrganizationId,
    string OrganizationName,
    string Objective,
    string ProposedProjectName,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> SuccessMeasures,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<AiAssistantConversationQuestionDto> Questions,
    string RulebookStatus,
    Guid? RuleSetId,
    int? RuleSetVersion,
    IReadOnlyList<OrganizationWorkRuleDecisionDto> RuleDecisions,
    IReadOnlyList<string> SourceRefs,
    string ActualProvider,
    string ActualModel,
    string PromptVersion,
    DateTimeOffset CreatedAt);
