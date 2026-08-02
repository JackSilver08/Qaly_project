using System.Text.Json;
using System.Text.Json.Nodes;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Infrastructure.Services.AI;

public static class DashboardStrategicBriefOutputContract
{
    private static readonly HashSet<string> MetricKeys = new(StringComparer.Ordinal)
    {
        "projectCount", "activeProjectCount", "riskProjectCount", "taskTotal", "done",
        "inProgress", "todo", "overdue", "dueSoon", "completionRate", "memberCount"
    };

    public static bool TryBuildResult(string providerContent, string snapshotJson, out string resultJson, out string? errorMessage)
    {
        resultJson = string.Empty;
        if (!TryReadSnapshot(snapshotJson, out var snapshot, out var sourceKeys, out errorMessage)) return false;
        try
        {
            using var providerDocument = JsonDocument.Parse(providerContent);
            var provider = providerDocument.RootElement;
            if (!HasOnlyProperties(provider, "summaryPoints", "risks", "priorities") ||
                !TryValidateItems(provider, "summaryPoints", ["text", "metricRefs", "sourceRefs"], sourceKeys, requireOne: true, out errorMessage) ||
                !TryValidateItems(provider, "risks", ["severity", "title", "metricRefs", "sourceRefs"], sourceKeys, requireOne: false, out errorMessage) ||
                !TryValidateItems(provider, "priorities", ["title", "rationale", "metricRefs", "sourceRefs"], sourceKeys, requireOne: false, out errorMessage))
            {
                errorMessage ??= "Provider output violates dashboard_strategic_brief.v1.";
                return false;
            }
            foreach (var risk in provider.GetProperty("risks").EnumerateArray())
            {
                if (risk.GetProperty("severity").GetString() is not ("low" or "medium" or "high"))
                {
                    errorMessage = "Risk severity must be low, medium, or high.";
                    return false;
                }
            }

            var result = new JsonObject
            {
                ["schemaId"] = DashboardStrategicBriefAiContract.SchemaId,
                ["organizationId"] = snapshot.GetProperty("organizationId").GetString(),
                ["requestedById"] = snapshot.GetProperty("requestedById").GetString(),
                ["snapshotAt"] = snapshot.GetProperty("snapshotAt").GetString(),
                ["coverage"] = CloneNode(snapshot.GetProperty("coverage")),
                ["metrics"] = CloneNode(snapshot.GetProperty("metrics")),
                ["summaryPoints"] = CloneNode(provider.GetProperty("summaryPoints")),
                ["risks"] = CloneNode(provider.GetProperty("risks")),
                ["priorities"] = CloneNode(provider.GetProperty("priorities")),
                ["sourceRefs"] = CloneNode(snapshot.GetProperty("sourceRefs")),
                ["warnings"] = new JsonArray()
            };
            resultJson = result.ToJsonString();
            return TryValidateFinal(resultJson, out errorMessage);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid strategic brief output: {exception.Message}";
            return false;
        }
    }

    public static bool TryValidateFinal(string content, out string? errorMessage)
    {
        errorMessage = null;
        if (!SchemaArtifactIsAvailable(out errorMessage)) return false;
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (!HasOnlyProperties(root, "schemaId", "organizationId", "requestedById", "snapshotAt", "coverage", "metrics", "summaryPoints", "risks", "priorities", "sourceRefs", "warnings") ||
                root.GetProperty("schemaId").GetString() != DashboardStrategicBriefAiContract.SchemaId ||
                !Guid.TryParse(root.GetProperty("organizationId").GetString(), out _) ||
                !Guid.TryParse(root.GetProperty("requestedById").GetString(), out _) ||
                !root.GetProperty("snapshotAt").TryGetDateTimeOffset(out _) ||
                !TryReadSnapshot(content, out _, out var sourceKeys, out errorMessage) ||
                !TryValidateItems(root, "summaryPoints", ["text", "metricRefs", "sourceRefs"], sourceKeys, requireOne: true, out errorMessage) ||
                !TryValidateItems(root, "risks", ["severity", "title", "metricRefs", "sourceRefs"], sourceKeys, requireOne: false, out errorMessage) ||
                !TryValidateItems(root, "priorities", ["title", "rationale", "metricRefs", "sourceRefs"], sourceKeys, requireOne: false, out errorMessage))
            {
                errorMessage ??= "Final strategic brief violates dashboard_strategic_brief.v1.";
                return false;
            }
            foreach (var risk in root.GetProperty("risks").EnumerateArray())
            {
                if (risk.GetProperty("severity").GetString() is not ("low" or "medium" or "high"))
                {
                    errorMessage = "Risk severity must be low, medium, or high.";
                    return false;
                }
            }
            return root.GetProperty("warnings").ValueKind == JsonValueKind.Array &&
                   root.GetProperty("warnings").EnumerateArray().All(item => item.ValueKind == JsonValueKind.String);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid final strategic brief: {exception.Message}";
            return false;
        }
    }

    private static bool TryReadSnapshot(string content, out JsonElement snapshot, out HashSet<string> sourceKeys, out string? errorMessage)
    {
        snapshot = default;
        sourceKeys = new HashSet<string>(StringComparer.Ordinal);
        errorMessage = null;
        try
        {
            using var document = JsonDocument.Parse(content);
            snapshot = document.RootElement.Clone();
            if (snapshot.TryGetProperty("schemaId", out var schemaId) && schemaId.GetString() == DashboardStrategicBriefAiContract.SchemaId)
            {
                // Final results contain the same server-owned snapshot fields plus model output.
            }
            else if (!HasOnlyProperties(snapshot, "schemaId", "organizationId", "requestedById", "snapshotAt", "coverage", "metrics", "sourceRefs") ||
                     schemaId.GetString() != DashboardStrategicBriefAiContract.SnapshotSchemaId)
            {
                errorMessage = "The server-owned strategic snapshot is incomplete.";
                return false;
            }
            if (!Guid.TryParse(snapshot.GetProperty("organizationId").GetString(), out _) ||
                !Guid.TryParse(snapshot.GetProperty("requestedById").GetString(), out _) ||
                !snapshot.GetProperty("snapshotAt").TryGetDateTimeOffset(out _) ||
                !TryValidateCoverageAndMetrics(snapshot, out errorMessage) ||
                snapshot.GetProperty("sourceRefs").ValueKind != JsonValueKind.Array)
            {
                errorMessage ??= "The server-owned strategic snapshot is invalid.";
                return false;
            }
            foreach (var source in snapshot.GetProperty("sourceRefs").EnumerateArray())
            {
                if (!HasOnlyProperties(source, "key", "type", "entityId", "projectId", "label", "url", "version") ||
                    !Guid.TryParse(source.GetProperty("entityId").GetString(), out var entityId) ||
                    !Guid.TryParse(source.GetProperty("projectId").GetString(), out var projectId) ||
                    string.IsNullOrWhiteSpace(source.GetProperty("label").GetString()))
                {
                    errorMessage = "A strategic source reference is malformed.";
                    return false;
                }
                var type = source.GetProperty("type").GetString();
                var key = type == "project" ? $"project:{entityId:D}" : type == "task" ? $"task:{entityId:D}" : null;
                var url = type == "project" ? $"/projects/{entityId:D}" : type == "task" ? $"/projects/{projectId:D}/tasks/{entityId:D}" : null;
                if (key == null || source.GetProperty("key").GetString() != key || source.GetProperty("url").GetString() != url || !sourceKeys.Add(key))
                {
                    errorMessage = "A strategic source reference is outside the authorized snapshot.";
                    return false;
                }
            }
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid strategic snapshot: {exception.Message}";
            return false;
        }
    }

    private static bool TryValidateCoverageAndMetrics(JsonElement root, out string? errorMessage)
    {
        errorMessage = null;
        var coverage = root.GetProperty("coverage");
        var metrics = root.GetProperty("metrics");
        if (!HasOnlyProperties(coverage, "visibleProjectCount", "includedTaskCount", "excludedPrivateTaskCount", "visibility") ||
            coverage.GetProperty("visibility").GetString() != "authorized_tenant_non_private" ||
            !HasOnlyProperties(metrics, MetricKeys.ToArray()))
        {
            errorMessage = "Strategic coverage or metrics do not match the locked contract.";
            return false;
        }
        var projectCount = metrics.GetProperty("projectCount").GetInt32();
        var taskTotal = metrics.GetProperty("taskTotal").GetInt32();
        var done = metrics.GetProperty("done").GetInt32();
        var inProgress = metrics.GetProperty("inProgress").GetInt32();
        var todo = metrics.GetProperty("todo").GetInt32();
        var rate = metrics.GetProperty("completionRate").GetDecimal();
        if (projectCount < 1 || taskTotal < 0 || done + inProgress + todo != taskTotal || rate is < 0 or > 100 ||
            coverage.GetProperty("visibleProjectCount").GetInt32() != projectCount ||
            coverage.GetProperty("includedTaskCount").GetInt32() != taskTotal ||
            coverage.GetProperty("excludedPrivateTaskCount").GetInt32() < 0)
        {
            errorMessage = "Strategic metrics are internally inconsistent.";
            return false;
        }
        var expectedRate = taskTotal == 0 ? 0m : Math.Round(done * 100m / taskTotal, 2, MidpointRounding.AwayFromZero);
        if (expectedRate != rate)
        {
            errorMessage = "completionRate does not match server-owned task counts.";
            return false;
        }
        return MetricKeys.All(key => metrics.GetProperty(key).ValueKind == JsonValueKind.Number);
    }

    private static bool TryValidateItems(JsonElement root, string property, IReadOnlyList<string> expected, IReadOnlySet<string> sources, bool requireOne, out string? errorMessage)
    {
        errorMessage = null;
        if (!root.TryGetProperty(property, out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 6 || (requireOne && items.GetArrayLength() == 0))
        {
            errorMessage = $"{property} must be a bounded array.";
            return false;
        }
        foreach (var item in items.EnumerateArray())
        {
            if (!HasOnlyProperties(item, expected.ToArray()))
            {
                errorMessage = $"{property} contains an item outside the locked contract.";
                return false;
            }
            foreach (var name in expected.Where(name => name is not ("metricRefs" or "sourceRefs")))
            {
                if (item.GetProperty(name).ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetProperty(name).GetString()) || item.GetProperty(name).GetString()!.Length > 800)
                {
                    errorMessage = $"{property}.{name} must be a bounded non-empty string.";
                    return false;
                }
            }
            if (!TryRefs(item, "metricRefs", MetricKeys, out var metricCount) || !TryRefs(item, "sourceRefs", sources, out var sourceCount) || metricCount + sourceCount == 0)
            {
                errorMessage = $"{property} contains an unknown grounding reference.";
                return false;
            }
        }
        return true;
    }

    private static bool TryRefs(JsonElement item, string property, IReadOnlySet<string> allowed, out int count)
    {
        count = 0;
        if (!item.TryGetProperty(property, out var refs) || refs.ValueKind != JsonValueKind.Array || refs.GetArrayLength() > 8) return false;
        var values = refs.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() : null).ToList();
        if (values.Any(value => value == null || !allowed.Contains(value)) || values.Distinct().Count() != values.Count) return false;
        count = values.Count;
        return true;
    }

    private static bool HasOnlyProperties(JsonElement element, params string[] expected)
        => element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected);

    private static JsonNode? CloneNode(JsonElement element) => JsonNode.Parse(element.GetRawText());

    private static bool SchemaArtifactIsAvailable(out string? errorMessage)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "ai", "dashboard_strategic_brief.schema.json");
        var workspacePath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "schemas", "ai", "dashboard_strategic_brief.schema.json");
        var path = File.Exists(outputPath) ? outputPath : workspacePath;
        if (!File.Exists(path)) { errorMessage = "dashboard_strategic_brief.v1 schema artifact is missing."; return false; }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("$id", out var id) || id.GetString() != DashboardStrategicBriefAiContract.SchemaId)
            { errorMessage = "dashboard_strategic_brief schema artifact has the wrong $id."; return false; }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        { errorMessage = $"dashboard_strategic_brief schema artifact cannot be read: {exception.Message}"; return false; }
        errorMessage = null;
        return true;
    }
}
