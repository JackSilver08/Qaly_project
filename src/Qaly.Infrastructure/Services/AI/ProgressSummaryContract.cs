using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qaly.Infrastructure.Services.AI;

public static class ProgressSummaryContract
{
    public const string SchemaId = "progress_summary.v4";

    private static readonly HashSet<string> MetricKeys = new(StringComparer.Ordinal)
    {
        "total", "done", "inProgress", "todo", "overdue", "dueSoon", "completionRate"
    };

    public static bool TryBuildResult(
        string providerContent,
        string snapshotJson,
        out string resultJson,
        out string? errorMessage)
    {
        resultJson = string.Empty;
        errorMessage = null;
        if (!TryReadSnapshot(snapshotJson, out var snapshot, out var sourceKeys, out errorMessage))
        {
            return false;
        }

        try
        {
            using var providerDocument = JsonDocument.Parse(providerContent);
            var provider = providerDocument.RootElement;
            if (!HasOnlyProperties(provider, "summaryPoints", "risks", "nextActions"))
            {
                errorMessage = "Provider output must contain exactly summaryPoints, risks, and nextActions.";
                return false;
            }

            if (!TryValidateGroundedArray(
                    provider,
                    "summaryPoints",
                    ["text", "metricRefs", "sourceRefs"],
                    sourceKeys,
                    requireAtLeastOneItem: true,
                    out errorMessage) ||
                !TryValidateGroundedArray(
                    provider,
                    "risks",
                    ["code", "severity", "title", "metricRefs", "sourceRefs"],
                    sourceKeys,
                    requireAtLeastOneItem: false,
                    out errorMessage) ||
                !TryValidateGroundedArray(
                    provider,
                    "nextActions",
                    ["title", "rationale", "metricRefs", "sourceRefs"],
                    sourceKeys,
                    requireAtLeastOneItem: false,
                    out errorMessage))
            {
                return false;
            }

            foreach (var risk in provider.GetProperty("risks").EnumerateArray())
            {
                var severity = risk.GetProperty("severity").GetString();
                if (severity is not ("low" or "medium" or "high"))
                {
                    errorMessage = "Risk severity must be low, medium, or high.";
                    return false;
                }
            }

            var result = new JsonObject
            {
                ["scope"] = CloneNode(snapshot.GetProperty("scope")),
                ["period"] = CloneNode(snapshot.GetProperty("period")),
                ["coverage"] = CloneNode(snapshot.GetProperty("coverage")),
                ["metrics"] = CloneNode(snapshot.GetProperty("metrics")),
                ["summaryPoints"] = CloneNode(provider.GetProperty("summaryPoints")),
                ["risks"] = CloneNode(provider.GetProperty("risks")),
                ["nextActions"] = CloneNode(provider.GetProperty("nextActions")),
                ["sourceRefs"] = CloneNode(snapshot.GetProperty("sourceRefs")),
                ["warnings"] = new JsonArray()
            };
            resultJson = result.ToJsonString();
            return TryValidateFinal(resultJson, out errorMessage);
        }
        catch (JsonException ex)
        {
            errorMessage = $"Invalid progress summary JSON: {ex.Message}";
            return false;
        }
    }

    public static bool TryBuildEmptyResult(
        string snapshotJson,
        out string resultJson,
        out string? errorMessage)
    {
        resultJson = string.Empty;
        if (!TryReadSnapshot(snapshotJson, out var snapshot, out _, out errorMessage))
        {
            return false;
        }

        if (snapshot.GetProperty("metrics").GetProperty("total").GetInt32() != 0)
        {
            errorMessage = "The deterministic empty result can only be used when total is zero.";
            return false;
        }

        var result = new JsonObject
        {
            ["scope"] = CloneNode(snapshot.GetProperty("scope")),
            ["period"] = CloneNode(snapshot.GetProperty("period")),
            ["coverage"] = CloneNode(snapshot.GetProperty("coverage")),
            ["metrics"] = CloneNode(snapshot.GetProperty("metrics")),
            ["summaryPoints"] = new JsonArray(),
            ["risks"] = new JsonArray(),
            ["nextActions"] = new JsonArray(),
            ["sourceRefs"] = CloneNode(snapshot.GetProperty("sourceRefs")),
            ["warnings"] = new JsonArray("NO_PROGRESS_TASKS")
        };
        resultJson = result.ToJsonString();
        return TryValidateFinal(resultJson, out errorMessage);
    }

    public static bool TryValidateFinal(string content, out string? errorMessage)
    {
        errorMessage = null;
        if (!SchemaArtifactIsAvailable(out errorMessage)) return false;

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (!HasOnlyProperties(
                    root,
                    "scope",
                    "period",
                    "coverage",
                    "metrics",
                    "summaryPoints",
                    "risks",
                    "nextActions",
                    "sourceRefs",
                    "warnings"))
            {
                errorMessage = "Final progress_summary.v4 root does not match the locked schema.";
                return false;
            }

            if (!TryReadSnapshot(content, out _, out var sourceKeys, out errorMessage))
            {
                return false;
            }

            var metrics = root.GetProperty("metrics");
            var total = metrics.GetProperty("total").GetInt32();
            var done = metrics.GetProperty("done").GetInt32();
            var inProgress = metrics.GetProperty("inProgress").GetInt32();
            var todo = metrics.GetProperty("todo").GetInt32();
            var completionRate = metrics.GetProperty("completionRate").GetDecimal();
            if (new[]
                {
                    total,
                    done,
                    inProgress,
                    todo,
                    metrics.GetProperty("overdue").GetInt32(),
                    metrics.GetProperty("dueSoon").GetInt32()
                }.Any(value => value < 0) ||
                total != done + inProgress + todo ||
                completionRate is < 0 or > 100)
            {
                errorMessage = "Server-owned progress metrics are internally inconsistent.";
                return false;
            }

            var expectedRate = total == 0
                ? 0m
                : Math.Round(done * 100m / total, 2, MidpointRounding.AwayFromZero);
            if (completionRate != expectedRate)
            {
                errorMessage = "completionRate does not match the server-owned task counts.";
                return false;
            }
            var coverage = root.GetProperty("coverage");
            if (coverage.GetProperty("includedTaskCount").GetInt32() != total ||
                coverage.GetProperty("excludedTaskCount").GetInt32() < 0 ||
                coverage.GetProperty("dataState").GetString() != (total == 0 ? "empty" : "sufficient"))
            {
                errorMessage = "coverage does not match the server-owned task counts.";
                return false;
            }

            if (!TryValidateGroundedArray(
                    root,
                    "summaryPoints",
                    ["text", "metricRefs", "sourceRefs"],
                    sourceKeys,
                    requireAtLeastOneItem: total > 0,
                    out errorMessage) ||
                !TryValidateGroundedArray(
                    root,
                    "risks",
                    ["code", "severity", "title", "metricRefs", "sourceRefs"],
                    sourceKeys,
                    requireAtLeastOneItem: false,
                    out errorMessage) ||
                !TryValidateGroundedArray(
                    root,
                    "nextActions",
                    ["title", "rationale", "metricRefs", "sourceRefs"],
                    sourceKeys,
                    requireAtLeastOneItem: false,
                    out errorMessage))
            {
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

            if (!root.TryGetProperty("warnings", out var warnings) ||
                warnings.ValueKind != JsonValueKind.Array ||
                warnings.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            {
                errorMessage = "warnings must be a string array.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid progress_summary.v4 result: {ex.Message}";
            return false;
        }
    }

    public static bool SnapshotIsEmpty(string snapshotJson)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            return document.RootElement.GetProperty("metrics").GetProperty("total").GetInt32() == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryReadSnapshot(
        string content,
        out JsonElement snapshot,
        out HashSet<string> sourceKeys,
        out string? errorMessage)
    {
        snapshot = default;
        sourceKeys = new HashSet<string>(StringComparer.Ordinal);
        errorMessage = null;
        try
        {
            using var document = JsonDocument.Parse(content);
            snapshot = document.RootElement.Clone();
            if (snapshot.ValueKind != JsonValueKind.Object ||
                !snapshot.TryGetProperty("scope", out var scope) ||
                !snapshot.TryGetProperty("period", out var period) ||
                !snapshot.TryGetProperty("coverage", out var coverage) ||
                !snapshot.TryGetProperty("metrics", out var metrics) ||
                !snapshot.TryGetProperty("sourceRefs", out var sources))
            {
                errorMessage = "The server-owned progress snapshot is incomplete.";
                return false;
            }

            var isProjectScope = HasOnlyProperties(scope, "projectId", "projectName", "projectCode");
            var isSprintScope = HasOnlyProperties(
                scope,
                "type",
                "projectId",
                "projectName",
                "projectCode",
                "sprintId",
                "sprintName");
            if ((!isProjectScope && !isSprintScope) ||
                !Guid.TryParse(scope.GetProperty("projectId").GetString(), out var projectId) ||
                string.IsNullOrWhiteSpace(scope.GetProperty("projectName").GetString()) ||
                scope.GetProperty("projectCode").ValueKind != JsonValueKind.String ||
                (isSprintScope &&
                    (scope.GetProperty("type").GetString() != "sprint" ||
                     !Guid.TryParse(scope.GetProperty("sprintId").GetString(), out _) ||
                     string.IsNullOrWhiteSpace(scope.GetProperty("sprintName").GetString()))) ||
                !HasOnlyProperties(period, "kind", "snapshotAt") ||
                period.GetProperty("kind").GetString() != "current_snapshot" ||
                !period.GetProperty("snapshotAt").TryGetDateTimeOffset(out _) ||
                !HasOnlyProperties(coverage, "dataState", "visibility", "includedTaskCount", "excludedTaskCount") ||
                coverage.GetProperty("visibility").GetString() != "manager_full_project" ||
                coverage.GetProperty("dataState").GetString() is not ("empty" or "sufficient") ||
                coverage.GetProperty("includedTaskCount").GetInt32() < 0 ||
                coverage.GetProperty("excludedTaskCount").GetInt32() < 0 ||
                !HasOnlyProperties(metrics, MetricKeys.ToArray()) ||
                sources.ValueKind != JsonValueKind.Array ||
                sources.GetArrayLength() == 0)
            {
                errorMessage = "The server-owned progress snapshot violates progress_summary.v4.";
                return false;
            }

            var sprintId = isSprintScope
                ? Guid.Parse(scope.GetProperty("sprintId").GetString()!)
                : (Guid?)null;
            foreach (var source in sources.EnumerateArray())
            {
                if (!HasOnlyProperties(source, "key", "type", "entityId", "label", "url", "version") ||
                    !Guid.TryParse(source.GetProperty("entityId").GetString(), out var entityId) ||
                    string.IsNullOrWhiteSpace(source.GetProperty("label").GetString()) ||
                    string.IsNullOrWhiteSpace(source.GetProperty("url").GetString()) ||
                    source.GetProperty("version").ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    errorMessage = "A source reference is invalid.";
                    return false;
                }

                var key = source.GetProperty("key").GetString();
                var type = source.GetProperty("type").GetString();
                var expectedKey = type switch
                {
                    "project" => $"project:{entityId:D}",
                    "sprint" => $"sprint:{entityId:D}",
                    "task" => $"task:{entityId:D}",
                    _ => null
                };
                var expectedUrl = type switch
                {
                    "project" when entityId == projectId => $"/projects/{projectId:D}",
                    "sprint" when entityId == sprintId => $"/projects/{projectId:D}#milestone-{entityId:D}",
                    "task" => $"/projects/{projectId:D}/tasks/{entityId:D}",
                    _ => null
                };
                if (string.IsNullOrWhiteSpace(key) ||
                    !string.Equals(key, expectedKey, StringComparison.Ordinal) ||
                    !string.Equals(source.GetProperty("url").GetString(), expectedUrl, StringComparison.Ordinal) ||
                    !sourceKeys.Add(key))
                {
                    errorMessage = "Source reference keys must be non-empty and unique.";
                    return false;
                }
            }

            var requiredScopeSource = sprintId.HasValue
                ? $"sprint:{sprintId.Value:D}"
                : $"project:{projectId:D}";
            if (!sourceKeys.Contains(requiredScopeSource))
            {
                errorMessage = "The scope source reference is required.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid server-owned progress snapshot: {ex.Message}";
            return false;
        }
    }

    private static bool TryValidateGroundedArray(
        JsonElement root,
        string propertyName,
        IReadOnlyList<string> requiredProperties,
        IReadOnlySet<string> sourceKeys,
        bool requireAtLeastOneItem,
        out string? errorMessage)
    {
        errorMessage = null;
        if (!root.TryGetProperty(propertyName, out var items) ||
            items.ValueKind != JsonValueKind.Array ||
            items.GetArrayLength() > 6 ||
            (requireAtLeastOneItem && items.GetArrayLength() == 0))
        {
            errorMessage = $"{propertyName} must be an array with the permitted item count.";
            return false;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!HasOnlyProperties(item, requiredProperties.ToArray()))
            {
                errorMessage = $"{propertyName} contains an item outside the locked contract.";
                return false;
            }

            foreach (var name in requiredProperties.Where(name => name is not ("metricRefs" or "sourceRefs")))
            {
                if (!item.TryGetProperty(name, out var value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()) ||
                    value.GetString()!.Length > 800)
                {
                    errorMessage = $"{propertyName}.{name} must be a non-empty bounded string.";
                    return false;
                }
            }

            if (!TryReadRefs(item, "metricRefs", MetricKeys, out var metricCount) ||
                !TryReadRefs(item, "sourceRefs", sourceKeys, out var sourceCount))
            {
                errorMessage = $"{propertyName} contains an unknown or malformed grounding reference.";
                return false;
            }

            if (metricCount + sourceCount == 0)
            {
                errorMessage = $"{propertyName} items require at least one metric or source reference.";
                return false;
            }
        }

        return true;
    }

    private static bool TryReadRefs(
        JsonElement item,
        string propertyName,
        IReadOnlySet<string> allowed,
        out int count)
    {
        count = 0;
        if (!item.TryGetProperty(propertyName, out var refs) ||
            refs.ValueKind != JsonValueKind.Array ||
            refs.GetArrayLength() > 8)
        {
            return false;
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in refs.EnumerateArray())
        {
            var key = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (string.IsNullOrWhiteSpace(key) || !allowed.Contains(key) || !unique.Add(key))
            {
                return false;
            }
        }

        count = unique.Count;
        return true;
    }

    private static bool HasOnlyProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var actual = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        return actual.SetEquals(expected);
    }

    private static JsonNode? CloneNode(JsonElement element)
        => JsonNode.Parse(element.GetRawText());

    private static bool SchemaArtifactIsAvailable(out string? errorMessage)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "ai", "progress_summary.schema.json");
        var workspacePath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "schemas", "ai", "progress_summary.schema.json");
        var path = File.Exists(outputPath) ? outputPath : workspacePath;
        if (!File.Exists(path))
        {
            errorMessage = "progress_summary.v4 schema artifact is missing.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("$id", out var id) ||
                !string.Equals(id.GetString(), SchemaId, StringComparison.Ordinal))
            {
                errorMessage = "progress_summary schema artifact has the wrong $id.";
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            errorMessage = $"progress_summary schema artifact cannot be read: {ex.Message}";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
