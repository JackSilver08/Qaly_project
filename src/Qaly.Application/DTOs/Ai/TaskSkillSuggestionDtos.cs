namespace Qaly.Application.DTOs.Ai;

public static class TaskSkillAiContract
{
    public const string JobType = "task_skill_suggestion";
    public const string SchemaId = "task_skill_suggestion.v1";
    public const string SnapshotSchemaId = "task_skill_suggestion.snapshot.v1";
    public const string DraftType = "TaskSkillSuggestion";
    public const string ConfirmAction = "apply_task_skills";
}

public sealed record TaskSkillSuggestionSnapshotDto(
    string SchemaId,
    TaskSkillSuggestionTaskContextDto Task,
    string SourceVersion,
    string CatalogVersion,
    string Language,
    IReadOnlyList<TaskSkillCatalogItemDto> Skills);

public sealed record TaskSkillSuggestionTaskContextDto(
    Guid Id,
    Guid ProjectId,
    Guid OrganizationId,
    string TaskRowVersion,
    string Title,
    string? Description,
    string Priority,
    bool IsPrivate,
    string SourceRef);

public sealed record TaskSkillCatalogItemDto(
    Guid Id,
    string Name,
    string? Description);

public sealed record TaskSkillSuggestionOutputDto(
    string SchemaId,
    Guid TaskId,
    string SourceVersion,
    string DataState,
    IReadOnlyList<TaskSkillSuggestionItemDto> Suggestions,
    IReadOnlyList<string> UnmappedTerms,
    DateTimeOffset GeneratedAt);

public sealed record TaskSkillSuggestionItemDto(
    Guid SkillId,
    string CanonicalName,
    string RequiredLevel,
    decimal Confidence,
    string Rationale,
    IReadOnlyList<string> SourceRefs);
