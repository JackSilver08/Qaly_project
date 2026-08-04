using System.Text.Json;
using System.Text.Json.Nodes;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Infrastructure.Services.AI;

public static class GroupSummaryOutputContract
{
    public static bool TryBuildResult(
        string providerContent,
        string snapshotJson,
        out string resultJson,
        out string? errorMessage)
    {
        resultJson = string.Empty;
        if (!TryReadSnapshot(snapshotJson, out var snapshot, out var allowedRefs, out errorMessage))
        {
            return false;
        }

        try
        {
            using var providerDocument = JsonDocument.Parse(providerContent);
            var provider = providerDocument.RootElement;
            if (!HasOnlyProperties(provider, "summary", "summarySourceRefs", "keyDecisions", "openQuestions", "actionCandidates") ||
                !TryReadBoundedString(provider, "summary", 2_000, out _) ||
                !TryReadRefs(provider, "summarySourceRefs", allowedRefs, requireOne: true, out errorMessage) ||
                !TryValidateItems(provider, "keyDecisions", ["text", "sourceRefs"], allowedRefs, out errorMessage) ||
                !TryValidateItems(provider, "openQuestions", ["text", "sourceRefs"], allowedRefs, out errorMessage) ||
                !TryValidateItems(provider, "actionCandidates", ["title", "details", "sourceRefs"], allowedRefs, out errorMessage))
            {
                errorMessage ??= "Provider output does not match group_selected_summary.v1.";
                return false;
            }

            var messageIds = snapshot.GetProperty("messageIds");
            var result = new JsonObject
            {
                ["schemaId"] = GroupSummaryAiContract.SchemaId,
                ["groupId"] = snapshot.GetProperty("groupId").GetString(),
                ["projectId"] = snapshot.GetProperty("projectId").GetString(),
                ["messageRange"] = new JsonObject
                {
                    ["messageIds"] = CloneNode(messageIds),
                    ["fromMessageId"] = messageIds[0].GetString(),
                    ["toMessageId"] = messageIds[messageIds.GetArrayLength() - 1].GetString()
                },
                ["summary"] = provider.GetProperty("summary").GetString(),
                ["summarySourceRefs"] = CloneNode(provider.GetProperty("summarySourceRefs")),
                ["keyDecisions"] = CloneNode(provider.GetProperty("keyDecisions")),
                ["openQuestions"] = CloneNode(provider.GetProperty("openQuestions")),
                ["actionCandidates"] = CloneNode(provider.GetProperty("actionCandidates")),
                ["sourceRefs"] = CloneNode(snapshot.GetProperty("sourceRefs")),
                ["warnings"] = new JsonArray()
            };
            resultJson = result.ToJsonString();
            return TryValidateFinal(resultJson, out errorMessage);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid group summary output: {exception.Message}";
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
            if (!HasOnlyProperties(
                    root,
                    "schemaId",
                    "groupId",
                    "projectId",
                    "messageRange",
                    "summary",
                    "summarySourceRefs",
                    "keyDecisions",
                    "openQuestions",
                    "actionCandidates",
                    "sourceRefs",
                    "warnings") ||
                root.GetProperty("schemaId").GetString() != GroupSummaryAiContract.SchemaId ||
                !Guid.TryParse(root.GetProperty("groupId").GetString(), out _) ||
                !Guid.TryParse(root.GetProperty("projectId").GetString(), out _) ||
                !TryReadBoundedString(root, "summary", 2_000, out _))
            {
                errorMessage = "Final group summary root violates group_selected_summary.v1.";
                return false;
            }

            var sourceRefs = root.GetProperty("sourceRefs");
            if (sourceRefs.ValueKind != JsonValueKind.Array || sourceRefs.GetArrayLength() is < 1 or > 50)
            {
                errorMessage = "sourceRefs must contain between 1 and 50 authorized messages.";
                return false;
            }

            var allowedRefs = new HashSet<string>(StringComparer.Ordinal);
            var orderedIds = new List<Guid>();
            var groupId = root.GetProperty("groupId").GetGuid();
            foreach (var source in sourceRefs.EnumerateArray())
            {
                if (!HasOnlyProperties(source, "key", "messageId", "url") ||
                    !Guid.TryParse(source.GetProperty("messageId").GetString(), out var messageId))
                {
                    errorMessage = "A group summary source reference is malformed.";
                    return false;
                }
                var key = $"message:{messageId:D}";
                var url = $"/groups/{groupId:D}?messageId={messageId:D}";
                if (source.GetProperty("key").GetString() != key ||
                    source.GetProperty("url").GetString() != url ||
                    !allowedRefs.Add(key))
                {
                    errorMessage = "A group summary source reference is outside the authorized range.";
                    return false;
                }
                orderedIds.Add(messageId);
            }

            var range = root.GetProperty("messageRange");
            if (!HasOnlyProperties(range, "messageIds", "fromMessageId", "toMessageId") ||
                range.GetProperty("messageIds").ValueKind != JsonValueKind.Array ||
                range.GetProperty("messageIds").EnumerateArray().Select(item => item.GetGuid()).SequenceEqual(orderedIds) == false ||
                range.GetProperty("fromMessageId").GetGuid() != orderedIds[0] ||
                range.GetProperty("toMessageId").GetGuid() != orderedIds[^1] ||
                !TryReadRefs(root, "summarySourceRefs", allowedRefs, requireOne: true, out errorMessage) ||
                !TryValidateItems(root, "keyDecisions", ["text", "sourceRefs"], allowedRefs, out errorMessage) ||
                !TryValidateItems(root, "openQuestions", ["text", "sourceRefs"], allowedRefs, out errorMessage) ||
                !TryValidateItems(root, "actionCandidates", ["title", "details", "sourceRefs"], allowedRefs, out errorMessage))
            {
                errorMessage ??= "The selected message range or grounded items are invalid.";
                return false;
            }

            if (root.GetProperty("warnings").ValueKind != JsonValueKind.Array ||
                root.GetProperty("warnings").EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            {
                errorMessage = "warnings must be a string array.";
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid final group summary: {exception.Message}";
            return false;
        }
    }

    private static bool TryReadSnapshot(
        string content,
        out JsonElement snapshot,
        out HashSet<string> allowedRefs,
        out string? errorMessage)
    {
        snapshot = default;
        allowedRefs = new HashSet<string>(StringComparer.Ordinal);
        errorMessage = null;
        try
        {
            using var document = JsonDocument.Parse(content);
            snapshot = document.RootElement.Clone();
            if (!HasOnlyProperties(snapshot, "schemaId", "groupId", "projectId", "messageIds", "sourceRefs") ||
                snapshot.GetProperty("schemaId").GetString() != GroupSummaryAiContract.SnapshotSchemaId ||
                !Guid.TryParse(snapshot.GetProperty("groupId").GetString(), out var groupId) ||
                !Guid.TryParse(snapshot.GetProperty("projectId").GetString(), out _) ||
                snapshot.GetProperty("messageIds").ValueKind != JsonValueKind.Array ||
                snapshot.GetProperty("sourceRefs").ValueKind != JsonValueKind.Array)
            {
                errorMessage = "The server-owned group summary snapshot is incomplete.";
                return false;
            }

            var ids = snapshot.GetProperty("messageIds").EnumerateArray().Select(item => item.GetGuid()).ToList();
            var sources = snapshot.GetProperty("sourceRefs").EnumerateArray().ToList();
            if (ids.Count is < 1 or > 50 || ids.Distinct().Count() != ids.Count || sources.Count != ids.Count)
            {
                errorMessage = "The selected message range is invalid.";
                return false;
            }
            for (var index = 0; index < ids.Count; index++)
            {
                var messageId = ids[index];
                var source = sources[index];
                var key = $"message:{messageId:D}";
                if (!HasOnlyProperties(source, "key", "messageId", "url") ||
                    source.GetProperty("messageId").GetGuid() != messageId ||
                    source.GetProperty("key").GetString() != key ||
                    source.GetProperty("url").GetString() != $"/groups/{groupId:D}?messageId={messageId:D}" ||
                    !allowedRefs.Add(key))
                {
                    errorMessage = "The server-owned source allowlist is invalid.";
                    return false;
                }
            }
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid group summary snapshot: {exception.Message}";
            return false;
        }
    }

    private static bool TryValidateItems(
        JsonElement root,
        string propertyName,
        IReadOnlyList<string> properties,
        IReadOnlySet<string> allowedRefs,
        out string? errorMessage)
    {
        errorMessage = null;
        if (!root.TryGetProperty(propertyName, out var items) ||
            items.ValueKind != JsonValueKind.Array ||
            items.GetArrayLength() > 12)
        {
            errorMessage = $"{propertyName} must be a bounded array.";
            return false;
        }
        foreach (var item in items.EnumerateArray())
        {
            if (!HasOnlyProperties(item, properties.ToArray()) ||
                properties.Where(name => name != "sourceRefs").Any(name => !TryReadBoundedString(item, name, 800, out _)) ||
                !TryReadRefs(item, "sourceRefs", allowedRefs, requireOne: true, out errorMessage))
            {
                errorMessage ??= $"{propertyName} contains an invalid grounded item.";
                return false;
            }
        }
        return true;
    }

    private static bool TryReadRefs(
        JsonElement root,
        string propertyName,
        IReadOnlySet<string> allowedRefs,
        bool requireOne,
        out string? errorMessage)
    {
        errorMessage = null;
        if (!root.TryGetProperty(propertyName, out var refs) || refs.ValueKind != JsonValueKind.Array || refs.GetArrayLength() > 8)
        {
            errorMessage = $"{propertyName} must be a bounded array.";
            return false;
        }
        var values = refs.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null).ToList();
        if ((requireOne && values.Count == 0) || values.Any(value => value == null || !allowedRefs.Contains(value)) || values.Distinct().Count() != values.Count)
        {
            errorMessage = $"{propertyName} contains an unknown source reference.";
            return false;
        }
        return true;
    }

    private static bool TryReadBoundedString(JsonElement root, string propertyName, int maximumLength, out string? value)
    {
        value = root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
    }

    private static bool HasOnlyProperties(JsonElement element, params string[] expected)
        => element.ValueKind == JsonValueKind.Object &&
           element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected);

    private static JsonNode? CloneNode(JsonElement element) => JsonNode.Parse(element.GetRawText());

    private static bool SchemaArtifactIsAvailable(out string? errorMessage)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "ai", "group_selected_summary.schema.json");
        var workspacePath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "schemas", "ai", "group_selected_summary.schema.json");
        var path = File.Exists(outputPath) ? outputPath : workspacePath;
        if (!File.Exists(path))
        {
            errorMessage = "group_selected_summary.v1 schema artifact is missing.";
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("$id", out var id) || id.GetString() != GroupSummaryAiContract.SchemaId)
            {
                errorMessage = "group_selected_summary schema artifact has the wrong $id.";
                return false;
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            errorMessage = $"group_selected_summary schema artifact cannot be read: {exception.Message}";
            return false;
        }
        errorMessage = null;
        return true;
    }
}
