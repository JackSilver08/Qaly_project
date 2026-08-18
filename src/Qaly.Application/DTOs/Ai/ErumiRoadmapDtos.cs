namespace Qaly.Application.DTOs.Ai;

/// <summary>
/// Constants for Roadmap AI Fast Action types.
/// </summary>
public static class ErumiRoadmapActionType
{
    public const string ExpandPhase = "ExpandPhase";
    public const string AuditRisks = "AuditRisks";
    public const string AutoBalance = "AutoBalance";
    public const string BreakdownWBS = "BreakdownWBS";
    public const string Forecast = "Forecast";
    public const string ExecutiveBrief = "ExecutiveBrief";
}

/// <summary>
/// Request DTO for chatting with Erumi AI regarding the project roadmap.
/// </summary>
public record ErumiRoadmapChatRequestDto(
    Guid ProjectId,
    string UserMessage,
    string? ContextSprintName);

/// <summary>
/// Response DTO for roadmap AI chat interaction.
/// </summary>
public record ErumiRoadmapChatResponseDto(
    string ReplyMessage,
    bool HasRoadmapProposal,
    ErumiRoadmapDiffProposalDto? Proposal);

/// <summary>
/// Request DTO for triggering 1-Click Fast Access AI actions.
/// </summary>
public record ErumiRoadmapActionRequestDto(
    Guid ProjectId,
    string ActionType,
    string? UserPrompt,
    Guid? TargetSprintOrPhaseId,
    string? ContextSprintName);

/// <summary>
/// Proposed Diff snapshot representing AI-generated roadmap changes before approval.
/// </summary>
public record ErumiRoadmapDiffProposalDto(
    Guid SnapshotId,
    Guid ProjectId,
    string PhaseName,
    DateTimeOffset EstimatedStartDate,
    DateTimeOffset EstimatedEndDate,
    IReadOnlyList<ErumiTaskProposalDto> ProposedTasks,
    IReadOnlyList<ErumiWorkloadImpactDto> WorkloadImpacts,
    string Summary,
    double ConfidenceScore = 0.92,
    IReadOnlyList<ErumiRoadmapRiskDto>? IdentifiedRisks = null);

/// <summary>
/// Individual proposed task item within an AI roadmap proposal.
/// </summary>
public record ErumiTaskProposalDto(
    string Title,
    string Description,
    string Priority,
    int EstimatedHours,
    string RecommendedRole,
    Guid? RecommendedAssigneeId,
    string? RecommendedAssigneeName,
    string? DependencyNote = null);

/// <summary>
/// Workload and capacity impact calculation for each team member.
/// </summary>
public record ErumiWorkloadImpactDto(
    Guid MemberUserId,
    string MemberName,
    string CurrentRole,
    int CurrentWeeklyHours,
    int ProposedAdditionalHours,
    bool IsOverloaded,
    string WarningMessage);

/// <summary>
/// Risk or bottleneck identified by the AI radar.
/// </summary>
public record ErumiRoadmapRiskDto(
    string RiskType,
    string Severity,
    string Description,
    string MitigationAdvice,
    string? BlockedItemTitle = null);

/// <summary>
/// Simulation result for What-If scenario modeling.
/// </summary>
public record ErumiRoadmapSimulationResultDto(
    string ScenarioName,
    int CompletionDateDeltaDays,
    int TotalAdditionalHours,
    double ConfidencePercentage,
    IReadOnlyList<string> KeyTradeoffs,
    IReadOnlyList<ErumiWorkloadImpactDto> MemberImpacts,
    string RecommendationSummary);

/// <summary>
/// Executive Brief DTO for sharing clean client/stakeholder updates.
/// </summary>
public record ErumiRoadmapExecutiveBriefDto(
    Guid ProjectId,
    string ProjectName,
    string HealthStatus,
    double CompletionPercentage,
    IReadOnlyList<string> KeyAchievements,
    IReadOnlyList<string> UpcomingMilestones,
    IReadOnlyList<string> ExecutiveRisks,
    string FormattedMarkdownSummary,
    DateTimeOffset GeneratedAt);

/// <summary>
/// DTO sent by authorized Project Owner / PM to approve and apply an AI proposal.
/// </summary>
public record ApproveErumiRoadmapProposalDto(
    Guid SnapshotId,
    Guid ProjectId,
    List<ErumiTaskProposalDto> ApprovedTasks);

/// <summary>
/// DTO sent to rollback an applied proposal snapshot within the retention window.
/// </summary>
public record RollbackErumiRoadmapSnapshotDto(
    Guid SnapshotId,
    Guid ProjectId);
