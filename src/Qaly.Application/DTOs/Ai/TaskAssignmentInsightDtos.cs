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
    IReadOnlyList<TaskAssignmentCandidateDto> Candidates);

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
    IReadOnlyList<string> RecentSignals);
