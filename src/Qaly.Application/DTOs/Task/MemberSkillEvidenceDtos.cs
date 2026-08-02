namespace Qaly.Application.DTOs.Task;

public sealed record CompletionContributorCandidateDto(Guid UserId, string FullName, string? Email);

public sealed record TaskCompletionAttributionDto(
    Guid Id,
    Guid ContributorUserId,
    string ContributorName,
    string Status,
    DateTimeOffset CompletedAt,
    DateTimeOffset ConfirmedAt,
    Guid ConfirmedByUserId,
    string? CorrectionReason,
    DateTimeOffset? CorrectionRequestedAt,
    string RowVersion,
    bool CanRequestCorrection);

public sealed record TaskCompletionAttributionsDto(
    Guid TaskId,
    string TaskStatus,
    bool CanManage,
    bool IsEligibleForAttribution,
    string TaskRowVersion,
    IReadOnlyList<CompletionContributorCandidateDto> EligibleContributors,
    IReadOnlyList<TaskCompletionAttributionDto> Attributions,
    string? Notice);

public sealed record ReplaceTaskCompletionAttributionsDto(
    string TaskRowVersion,
    IReadOnlyList<Guid> ContributorUserIds,
    bool Confirmed,
    string? ConfirmationNote = null);

public sealed record RequestCompletionAttributionCorrectionDto(string RowVersion, string Reason);

public sealed record MemberSkillEvidenceSourceDto(
    Guid AttributionId,
    Guid TaskId,
    string? TaskTitle,
    string? TaskUrl,
    bool IsRestricted,
    DateTimeOffset CompletedAt,
    string RequiredLevel);

public sealed record MemberSkillEvidenceDto(
    Guid SkillId,
    string SkillName,
    string EvidenceBand,
    decimal Confidence,
    int VerifiedTaskCount,
    int RestrictedTaskCount,
    DateTimeOffset? MostRecentCompletedAt,
    bool IsStale,
    IReadOnlyList<MemberSkillEvidenceSourceDto> Sources);

public sealed record MemberSkillProfileDto(
    Guid OrganizationId,
    Guid MemberId,
    string MemberName,
    bool IsSelf,
    bool CanManageEvidence,
    string EvidenceMethodVersion,
    IReadOnlyList<MemberSkillEvidenceDto> Skills,
    int PendingCorrectionCount,
    string EmptyState);
