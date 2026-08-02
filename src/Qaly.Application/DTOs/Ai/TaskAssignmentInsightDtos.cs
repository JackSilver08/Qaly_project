namespace Qaly.Application.DTOs.Ai;

public record TaskAssignmentInsightDto(
    Guid TaskId,
    Guid ProjectId,
    string TaskTitle,
    string? TaskDescription,
    string TaskPriority,
    string TaskStatus,
    DateTimeOffset? DueDate,
    Guid? RecommendedUserId,
    string RecommendedUserName,
    string RecommendationSummary,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<TaskAssignmentCandidateDto> Candidates,
    string ScoringVersion = "legacy",
    string EvidenceState = "legacy",
    string WorkloadScope = "legacy_project_tasks",
    string TaskRowVersion = "",
    IReadOnlyList<string>? RequiredSkills = null);

public record TaskAssignmentCandidateDto(
    Guid UserId,
    string FullName,
    string Role,
    int ActiveTaskCount,
    int OverdueTaskCount,
    int RecentCompletionCount,
    int SkillMatchScore,
    int HistoryScore,
    int WorkloadScore,
    int TotalScore,
    IReadOnlyList<string> SkillSignals,
    IReadOnlyList<string> RecentSignals,
    int SkillCoveragePercent = 0,
    decimal EvidenceConfidence = 0,
    string EvidenceBand = "none",
    IReadOnlyList<string>? MissingSkills = null,
    int EvidenceSourceCount = 0,
    int RestrictedEvidenceCount = 0,
    IReadOnlyList<TaskAssignmentEvidenceSourceDto>? EvidenceSources = null);

public record TaskAssignmentEvidenceSourceDto(
    Guid TaskId,
    string TaskTitle,
    string TaskUrl,
    DateTimeOffset CompletedAt,
    IReadOnlyList<string> MatchedSkills);
