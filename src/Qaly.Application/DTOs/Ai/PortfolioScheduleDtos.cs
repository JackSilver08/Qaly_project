namespace Qaly.Application.DTOs.Ai;

public sealed record PortfolioCapacityDto(
    Guid ProjectId,
    Guid OrganizationId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string ScoringVersion,
    string VisibilityState,
    bool CanManageCapacity,
    bool CanGenerateProposal,
    IReadOnlyList<PortfolioMemberCapacityDto> Members,
    DateTimeOffset GeneratedAt);

public sealed record PortfolioMemberCapacityDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    decimal WeeklyCapacityHours,
    string CapacityState,
    decimal WindowCapacityHours,
    decimal AssignedHours,
    decimal RemainingHours,
    int UtilizationPercent,
    int OpenTaskCount,
    int MissingEstimateCount,
    int DeadlineCollisionCount,
    bool HasRestrictedLoad,
    IReadOnlyList<PortfolioProjectLoadDto> ProjectLoads,
    IReadOnlyList<MemberAvailabilityWindowDto> AvailabilityWindows,
    string? ProfileRowVersion);

public sealed record PortfolioProjectLoadDto(
    Guid ProjectId,
    string ProjectName,
    decimal AssignedHours,
    int OpenTaskCount,
    bool SourcesRestricted);

public sealed record MemberAvailabilityWindowDto(
    Guid? Id,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Kind,
    decimal? AvailableHours,
    string? RowVersion = null);

public sealed record UpdateMemberCapacityProfileDto(
    decimal WeeklyCapacityHours,
    string TimeZoneId,
    IReadOnlyList<MemberAvailabilityWindowDto> AvailabilityWindows,
    string? RowVersion,
    bool Confirmed = false);

public sealed record CreatePortfolioScheduleProposalDto(
    IReadOnlyList<Guid> TaskIds,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd);

public sealed record UpdatePortfolioScheduleProposalDto(
    IReadOnlyList<PortfolioScheduleProposalItemDto> Items,
    string RowVersion);

public sealed record ConfirmPortfolioScheduleProposalDto(
    IReadOnlyList<Guid> SelectedItemIds,
    string RowVersion,
    string IdempotencyKey,
    bool Confirmed = false);

public sealed record RejectPortfolioScheduleProposalDto(
    string Reason,
    string RowVersion,
    string IdempotencyKey);

public sealed record PortfolioScheduleProposalDto(
    Guid DraftId,
    Guid JobId,
    Guid ProjectId,
    Guid OrganizationId,
    string Status,
    string SchemaId,
    string ScoringVersion,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<PortfolioScheduleProposalItemDto> Items,
    IReadOnlyList<PortfolioScheduleSourceDto> Sources,
    IReadOnlyList<string> Warnings,
    string RowVersion,
    string ProviderName,
    string ModelName,
    DateTimeOffset GeneratedAt,
    PortfolioScheduleReceiptDto? Receipt = null);

public sealed record PortfolioScheduleProposalItemDto(
    Guid ItemId,
    Guid TaskId,
    string TaskTitle,
    Guid CurrentProjectId,
    string ProjectName,
    Guid? CurrentAssigneeId,
    string? CurrentAssigneeName,
    Guid ProposedAssigneeId,
    string ProposedAssigneeName,
    DateTimeOffset ProposedStart,
    DateTimeOffset ProposedDue,
    int SkillCoveragePercent,
    decimal EvidenceConfidence,
    decimal LoadBeforeHours,
    decimal LoadAfterHours,
    decimal CapacityHours,
    IReadOnlyList<string> DependencyConflicts,
    IReadOnlyList<string> DeadlineRisks,
    IReadOnlyList<PortfolioScheduleAlternativeDto> Alternatives,
    IReadOnlyList<string> SourceRefs,
    string TaskRowVersion,
    bool Selected = true,
    IReadOnlyList<string>? BlockingReasons = null);

public sealed record PortfolioScheduleAlternativeDto(
    Guid UserId,
    string FullName,
    int SkillCoveragePercent,
    decimal RemainingHours,
    string TradeOff,
    decimal EvidenceConfidence = 0m,
    decimal LoadBeforeHours = 0m,
    decimal CapacityHours = 0m,
    IReadOnlyList<string>? BlockingReasons = null);

public sealed record PortfolioScheduleSourceDto(
    string Key,
    string Type,
    Guid? EntityId,
    string Label,
    string? Url,
    bool Restricted);

public sealed record PortfolioScheduleReceiptDto(
    Guid DraftId,
    string ExecutionId,
    int AppliedCount,
    IReadOnlyList<Guid> AppliedTaskIds,
    IReadOnlyList<string> ReadBackLinks,
    DateTimeOffset ConfirmedAt,
    string Status = AiActionReceiptStatuses.VerificationPending,
    bool ReadBackVerified = false,
    IReadOnlyList<string>? VerificationErrors = null);
