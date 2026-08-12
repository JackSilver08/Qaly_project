using System.Text.Json;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public static class TaskDraftAiContract
{
    public const string JobType = "task_draft_native";
    public const string SchemaId = "task_draft.v5";
    public const string SnapshotSchemaId = "task_draft_source_snapshot.v1";
    public const string DraftType = "TaskDraft";
    public const string ConfirmAction = "create_tasks";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> Priorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low", "Medium", "High", "Critical"
    };

    public static bool TryBuildResult(
        string providerJson,
        string snapshotJson,
        out string normalizedJson,
        out string? error)
        => TryNormalize(providerJson, snapshotJson, requireSchema: true, out normalizedJson, out error);

    public static bool TryValidateReviewed(
        string reviewedJson,
        string snapshotJson,
        out string normalizedJson,
        out string? error)
        => TryNormalize(reviewedJson, snapshotJson, requireSchema: true, out normalizedJson, out error);

    private static bool TryNormalize(
        string payloadJson,
        string snapshotJson,
        bool requireSchema,
        out string normalizedJson,
        out string? error)
    {
        normalizedJson = string.Empty;
        error = null;
        try
        {
            var snapshot = JsonSerializer.Deserialize<TaskDraftSourceSnapshotDto>(snapshotJson, JsonOptions);
            var payload = JsonSerializer.Deserialize<AiTaskDraftPayload>(payloadJson, JsonOptions);
            if (snapshot == null || payload == null ||
                !string.Equals(snapshot.SchemaId, SnapshotSchemaId, StringComparison.Ordinal))
            {
                error = "The task draft source snapshot is invalid.";
                return false;
            }
            if (requireSchema && !string.Equals(payload.SchemaId, SchemaId, StringComparison.Ordinal))
            {
                error = $"schemaId must equal {SchemaId}.";
                return false;
            }
            if (payload.DataState is not ("ready" or "insufficient_evidence"))
            {
                error = "dataState must be ready or insufficient_evidence.";
                return false;
            }
            if (payload.Tasks.Count > 20 ||
                (payload.DataState == "ready" && payload.Tasks.Count == 0) ||
                (payload.DataState == "insufficient_evidence" && payload.Tasks.Count != 0))
            {
                error = "A ready task draft requires 1-20 tasks; insufficient_evidence requires an empty task list.";
                return false;
            }

            var allowedRefs = snapshot.Sources.Select(source => source.Ref).ToHashSet(StringComparer.Ordinal);
            var allowedMembers = snapshot.Members.Select(member => member.UserId).ToHashSet();
            var clientIds = new HashSet<string>(StringComparer.Ordinal);
            var normalizedTasks = new List<AiTaskDraftItem>(payload.Tasks.Count);
            for (var index = 0; index < payload.Tasks.Count; index++)
            {
                var item = payload.Tasks[index];
                var title = item.Title?.Trim() ?? string.Empty;
                var description = item.Description?.Trim();
                var clientId = string.IsNullOrWhiteSpace(item.ClientId)
                    ? $"draft-task-{index + 1}"
                    : item.ClientId.Trim();
                var refs = (item.SourceRefs ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (title.Length is < 3 or > 200 || (description?.Length ?? 0) > 4000)
                {
                    error = $"Task {index + 1} has an invalid title or description length.";
                    return false;
                }
                if (!Priorities.Contains(item.Priority) ||
                    !string.Equals(item.Status, "Todo", StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Task {index + 1} has an unsupported priority or status.";
                    return false;
                }
                if (!clientIds.Add(clientId) || clientId.Length > 100)
                {
                    error = "Task clientId values must be unique and at most 100 characters.";
                    return false;
                }
                if (item.Confidence is < 0m or > 1m)
                {
                    error = $"Task {index + 1} confidence must be between 0 and 1.";
                    return false;
                }
                if (refs.Count == 0 || refs.Any(sourceRef => !allowedRefs.Contains(sourceRef)))
                {
                    error = $"Task {index + 1} must cite only authorized selected-message source refs.";
                    return false;
                }
                if (item.AssigneeId.HasValue && !allowedMembers.Contains(item.AssigneeId.Value))
                {
                    error = $"Task {index + 1} references an unauthorized project member.";
                    return false;
                }

                normalizedTasks.Add(item with
                {
                    Title = title,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Priority = Priorities.First(priority => string.Equals(priority, item.Priority, StringComparison.OrdinalIgnoreCase)),
                    Status = "Todo",
                    ClientId = clientId,
                    Confidence = decimal.Round(item.Confidence, 4),
                    SourceRefs = refs
                });
            }

            normalizedJson = JsonSerializer.Serialize(
                new AiTaskDraftPayload(normalizedTasks, SchemaId, payload.DataState),
                JsonOptions);
            return true;
        }
        catch (JsonException exception)
        {
            error = $"Task draft JSON is invalid: {exception.Message}";
            return false;
        }
    }
}
