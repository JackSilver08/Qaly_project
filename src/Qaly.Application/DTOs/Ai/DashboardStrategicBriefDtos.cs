namespace Qaly.Application.DTOs.Ai;

public static class DashboardStrategicBriefAiContract
{
    public const string JobType = "dashboard_strategic_brief";
    public const string SchemaId = "dashboard_strategic_brief.v1";
    public const string SnapshotSchemaId = "dashboard_strategic_snapshot.v1";
}

public sealed record DashboardStrategicBriefRequestDto(
    Guid OrganizationId,
    string Language = "vi",
    string ProviderHint = "deepseek-chat",
    decimal? MaximumEstimatedCostUsd = null,
    string CacheMode = "use");
