namespace Qaly.Application.DTOs.Ai;

public static class AiAssistantGoalPlanningContract
{
    public const string SchemaId = "assistant_goal_analysis.v1";
    public const string WorkPlanSchemaId = "assistant_work_plan.v1";
    public const string PromptId = "assistant-goal-planner";
    public const string PromptVersion = "1.0.0";
    public const string ModelProfile = "reasoning_strong";
    public const int MaxSteps = 8;
    public const int MaxAttemptsPerStep = 2;
}

public sealed record AiAssistantGoalScopeDto(
    string ScopeType,
    Guid? ProjectId,
    string? EntityType,
    Guid? EntityId,
    string Label,
    double Confidence,
    string Reason);

public sealed record AiAssistantGoalUnknownDto(
    string UnknownId,
    string Question,
    bool Blocking);

public sealed record AiAssistantSkillCandidateDto(
    string SkillId,
    string FitReason,
    double Confidence);

public sealed record AiAssistantSkillSelectionDto(
    string SkillId,
    string Version,
    string Title,
    string FitReason,
    double Confidence,
    string RiskClass,
    string ConfirmationPolicy,
    string RendererId);

public sealed record AiAssistantMissingSkillDto(
    string SkillId,
    string Title,
    string Reason,
    string SuggestedPath);

public sealed record AiAssistantWorkPlanStepDto(
    string StepId,
    string Kind,
    string PublicLabel,
    string? SkillId,
    IReadOnlyList<string> SourceIds,
    IReadOnlyList<string> DependencyIds,
    string? ExpectedOutputSchemaId,
    IReadOnlyList<string> VerificationIds,
    string MutationClass,
    string State);

public sealed record AiAssistantWorkPlanDto(
    string SchemaId,
    string Objective,
    AiAssistantGoalScopeDto Scope,
    IReadOnlyList<string> SelectedSkillIds,
    IReadOnlyList<AiAssistantWorkPlanStepDto> Steps,
    IReadOnlyList<AiAssistantGoalUnknownDto> BlockingUnknowns,
    int MaxSteps,
    int MaxAttemptsPerStep,
    IReadOnlyList<string> StopConditions,
    bool RequiresPlanApproval);

public sealed record AiAssistantGoalAnalysisDto(
    string SchemaId,
    string PromptId,
    string PromptVersion,
    string Objective,
    string UserJob,
    IReadOnlyList<string> IntentFacets,
    IReadOnlyList<AiAssistantGoalScopeDto> Scopes,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<AiAssistantGoalUnknownDto> Unknowns,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<AiAssistantSkillSelectionDto> SelectedSkills,
    IReadOnlyList<AiAssistantMissingSkillDto> MissingSkills,
    string RiskLevel,
    bool RequiresConfirmation,
    string Disposition,
    double Confidence,
    IReadOnlyList<string> Warnings,
    string ActualProvider,
    string ActualModel,
    bool UsedFallback);

public sealed record AiAssistantGoalPlanningEnvelopeDto(
    string SchemaId,
    string PromptId,
    string PromptVersion,
    string Objective,
    string UserJob,
    IReadOnlyList<string> IntentFacets,
    IReadOnlyList<AiAssistantGoalScopeDto> Scopes,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<AiAssistantGoalUnknownDto> Unknowns,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<AiAssistantSkillCandidateDto> RankedSkills,
    IReadOnlyList<AiAssistantMissingSkillDto> MissingSkills,
    string RiskLevel,
    bool RequiresConfirmation,
    string Disposition,
    double Confidence,
    IReadOnlyList<string> Warnings,
    AiAssistantWorkPlanDto WorkPlan);

public sealed record AiAssistantGoalPlanningResultDto(
    AiAssistantGoalAnalysisDto GoalAnalysis,
    AiAssistantWorkPlanDto WorkPlan,
    string? SelectedCapabilityId,
    bool UsedFallback);

public sealed record AiAssistantGoalPlanningValidationContextDto(
    string Message,
    AiAssistantClientContextDto? ClientContext,
    string? RequestedCapabilityId,
    IReadOnlyList<AiAssistantCapabilityDescriptorDto> AvailableSkills);
