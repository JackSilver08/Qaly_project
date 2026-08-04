namespace Qaly.Application.DTOs.Ai;

public static class GroupSummaryAiContract
{
    public const string JobType = "group_selected_summary";
    public const string SchemaId = "group_selected_summary.v1";
    public const string SnapshotSchemaId = "group_selected_summary_snapshot.v1";
}

public sealed record GroupSummaryRequestDto(
    Guid ProjectId,
    IReadOnlyList<Guid> MessageIds,
    string Language = "vi",
    string ProviderHint = "deepseek-v4-pro",
    decimal? MaximumEstimatedCostUsd = null,
    string CacheMode = "use");

public sealed record GroupSummarySourceDto(
    string Key,
    Guid MessageId,
    string Url);

public sealed record GroupSummarySnapshotDto(
    string SchemaId,
    Guid GroupId,
    Guid ProjectId,
    IReadOnlyList<Guid> MessageIds,
    IReadOnlyList<GroupSummarySourceDto> SourceRefs);
