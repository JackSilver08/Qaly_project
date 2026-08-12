namespace Qaly.Application.DTOs.Ai;

public record ErumiChatRequestDto(
    Guid ProjectId,
    string UserMessage,
    string? ContextSprintName);

public record ErumiChatResponseDto(
    string ReplyMessage,
    bool HasRoadmapProposal,
    ErumiRoadmapDiffProposalDto? Proposal);

public record ErumiRoadmapDiffProposalDto(
    Guid SnapshotId,
    Guid ProjectId,
    string PhaseName,
    DateTimeOffset EstimatedStartDate,
    DateTimeOffset EstimatedEndDate,
    IReadOnlyList<ErumiTaskProposalDto> ProposedTasks,
    IReadOnlyList<ErumiWorkloadImpactDto> WorkloadImpacts,
    string Summary);

public record ErumiTaskProposalDto(
    string Title,
    string Description,
    string Priority,
    int EstimatedHours,
    string RecommendedRole,
    Guid? RecommendedAssigneeId,
    string? RecommendedAssigneeName);

public record ErumiWorkloadImpactDto(
    Guid MemberUserId,
    string MemberName,
    string CurrentRole,
    int CurrentWeeklyHours,
    int ProposedAdditionalHours,
    bool IsOverloaded,
    string WarningMessage);

public record ApproveErumiRoadmapProposalDto(
    Guid SnapshotId,
    Guid ProjectId,
    List<ErumiTaskProposalDto> ApprovedTasks);

public record RollbackErumiRoadmapSnapshotDto(
    Guid SnapshotId,
    Guid ProjectId);
