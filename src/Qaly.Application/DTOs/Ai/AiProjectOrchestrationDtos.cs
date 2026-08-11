namespace Qaly.Application.DTOs.Ai;

public static class AiProjectOrchestrationContract
{
    public const string StaffingCapabilityId = "project.staffing.plan.v1";
    public const string ExecuteCapabilityId = "project.launch.execute.v1";
    public const string MonitorCapabilityId = "project.operation.monitor.v1";
    public const string PlanningRequestSchemaId = "project_staffing_plan_request.v1";
    public const string ConfirmRequestSchemaId = "project_launch_confirm_request.v1";
    public const string MonitorRequestSchemaId = "project_operation_monitor_request.v1";
    public const string ModelPlanSchemaId = "project_launch_plan_model_output.v1";
    public const string StaffingSchemaId = "project_staffing_scenario.v1";
    public const string PlanSchemaId = "project_launch_plan.v1";
    public const string ExecutionReceiptSchemaId = "project_launch_execution_receipt.v1";
    public const string ReplanSchemaId = "project_replan_proposal.v1";
    public const string PlanRendererId = "project-launch-plan.v1";
    public const string PromptVersion = "project_launch_delivery_plan@1.0.0";
    public const string ScoringVersion = "project-staffing-deterministic@1.0.0";
}

public sealed record ProjectStaffingCandidateDto(
    Guid UserId,
    string DisplayName,
    string OrganizationRole,
    bool ManagerEligible,
    bool StaffingEligible,
    IReadOnlyList<string> HardRejects,
    IReadOnlyList<string> EvidenceSkills,
    decimal EvidenceConfidence,
    decimal WeeklyCapacityHours,
    decimal WindowCapacityHours,
    decimal ExistingCommittedHours,
    decimal FocusReserveHours,
    decimal AvailableHours,
    decimal ProposedHours,
    decimal LoadAfterPercent,
    int ActiveProjectCount,
    string TimeZoneId,
    string CapacityState,
    IReadOnlyList<string> SourceRefs);

public sealed record ProjectStaffingMemberDto(
    Guid UserId,
    string DisplayName,
    string ProposedRole,
    decimal ProposedHours,
    IReadOnlyList<string> CoveredSkills,
    IReadOnlyList<string> MissingSkills,
    decimal LoadAfterPercent,
    IReadOnlyList<string> DecisionReasons);

public sealed record ProjectStaffingScenarioDto(
    string ScenarioId,
    string Title,
    string Description,
    bool Feasible,
    decimal Score,
    Guid? ManagerUserId,
    string? ManagerName,
    IReadOnlyList<ProjectStaffingMemberDto> Members,
    IReadOnlyList<ProjectStaffingCandidateDto> ManagerCandidates,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<OrganizationWorkRuleDecisionDto> RuleDecisions,
    IReadOnlyList<string> SourceRefs,
    string ScoringVersion);

public sealed record ProjectLaunchTaskPlanDto(
    string ClientId,
    string Title,
    string Description,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> DefinitionOfDone,
    string Priority,
    int EstimatedHours,
    Guid? ProposedAssigneeId,
    Guid? ProposedReviewerId,
    IReadOnlyList<Guid> RequiredSkillIds,
    IReadOnlyList<string> RequiredSkillNames,
    IReadOnlyList<string> DependencyClientIds,
    IReadOnlyList<string> SourceRefs,
    bool Selected = true);

public sealed record ProjectLaunchSprintPlanDto(
    string ClientId,
    string Name,
    string Objective,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    IReadOnlyList<string> ExitCriteria,
    IReadOnlyList<ProjectLaunchTaskPlanDto> Tasks,
    bool Selected = true);

public sealed record ProjectLaunchDeliveryPlanDto(
    string ProposedProjectName,
    string ProposedProjectCode,
    string Objective,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> SuccessMeasures,
    IReadOnlyList<string> ArchitectureProposal,
    IReadOnlyList<ProjectLaunchSprintPlanDto> Sprints,
    IReadOnlyList<string> CriticalPathClientIds,
    IReadOnlyList<string> UnallocatedWork,
    IReadOnlyList<string> SkillGaps,
    IReadOnlyList<string> ScheduleRisks,
    IReadOnlyList<string> CollaborationProposal,
    IReadOnlyList<string> ExternalDeferred,
    IReadOnlyList<string> Assumptions);

public sealed record ProjectLaunchCommandReceiptDto(
    string CommandId,
    string AdapterId,
    string Status,
    string Summary,
    Guid? EntityId = null,
    string? DeepLink = null,
    string? ErrorCode = null);

public sealed record ProjectLaunchExecutionReceiptDto(
    Guid ReceiptId,
    string SchemaId,
    string State,
    Guid PlanId,
    Guid ProjectId,
    string IdempotencyKey,
    bool InternalTransactionCommitted,
    bool ReadBackVerified,
    IReadOnlyList<ProjectLaunchCommandReceiptDto> Commands,
    IReadOnlyList<string> CreatedEntityLinks,
    IReadOnlyList<string> DeferredExternalActions,
    bool RollbackAvailable,
    string? RollbackBlockReason,
    Guid? RuleSetId,
    int? RuleSetVersion,
    string SourceVersionHash,
    string ActualProvider,
    string ActualModel,
    DateTimeOffset ExecutedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? RolledBackAt,
    string? RollbackReason,
    long Revision);

public sealed record ProjectReplanChangeDto(
    string ChangeType,
    string Severity,
    string Summary,
    string BaselineValue,
    string CurrentValue,
    string SuggestedAction);

public sealed record ProjectReplanProposalDto(
    Guid ProposalId,
    string SchemaId,
    int Revision,
    string State,
    Guid PlanId,
    Guid ExecutionId,
    Guid ProjectId,
    IReadOnlyList<string> TriggerCodes,
    IReadOnlyList<ProjectReplanChangeDto> Changes,
    IReadOnlyList<string> BlockingUnknowns,
    IReadOnlyList<string> SourceRefs,
    string BaselineHash,
    string CurrentHash,
    bool RequiresConfirmation,
    DateTimeOffset CreatedAt,
    long RowRevision);

public sealed record ProjectLaunchPlanDto(
    Guid PlanId,
    string SchemaId,
    int Revision,
    string State,
    Guid BriefId,
    Guid OrganizationId,
    string OrganizationName,
    Guid? RuleSetId,
    int? RuleSetVersion,
    string ScoringVersion,
    string SourceVersionHash,
    IReadOnlyList<ProjectStaffingScenarioDto> StaffingScenarios,
    string? SelectedScenarioId,
    ProjectLaunchDeliveryPlanDto DeliveryPlan,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> SourceRefs,
    string ActualProvider,
    string ActualModel,
    string PromptVersion,
    DateTimeOffset CreatedAt,
    long RowRevision,
    ProjectLaunchExecutionReceiptDto? ExecutionReceipt = null,
    ProjectReplanProposalDto? LatestReplanProposal = null);

public sealed record ConfirmProjectLaunchPlanRequestDto(
    bool Confirmed,
    long ExpectedRevision,
    string SelectedScenarioId);

public sealed record RollbackProjectLaunchExecutionRequestDto(
    bool Confirmed,
    long ExpectedRevision,
    string Reason);

public sealed record MonitorProjectLaunchExecutionRequestDto(long ExpectedRevision);

public sealed record ProjectLaunchModelTaskDto(
    string ClientId,
    string Title,
    string Description,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> DefinitionOfDone,
    string Priority,
    int EstimatedHours,
    IReadOnlyList<string> RequiredSkillNames,
    IReadOnlyList<string> DependencyClientIds);

public sealed record ProjectLaunchModelSprintDto(
    string ClientId,
    string Name,
    string Objective,
    int StartWeek,
    int DurationWeeks,
    IReadOnlyList<string> ExitCriteria,
    IReadOnlyList<ProjectLaunchModelTaskDto> Tasks);

public sealed record ProjectLaunchModelOutputDto(
    IReadOnlyList<string> ArchitectureProposal,
    IReadOnlyList<ProjectLaunchModelSprintDto> Sprints,
    IReadOnlyList<string> CriticalPathClientIds,
    IReadOnlyList<string> CollaborationProposal,
    IReadOnlyList<string> ExternalDeferred,
    IReadOnlyList<string> Assumptions);
