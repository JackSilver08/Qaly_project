#pragma warning disable CA1822 // Mark members as static

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qaly.Infrastructure.Services.AI;

public class AiOutputValidator
{
    public bool Validate(string content, string schemaId, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "Response content is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (string.Equals(schemaId, "WorkspaceStrategy.v1", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "WorkspaceStrategy.v1 root must be a JSON object.";
                    return false;
                }

                if (!TryReadNonEmptyString(root, "summary", out var summary) ||
                    !TryReadStringArray(root, "riskAnalysis", out var risks) ||
                    !TryReadStringArray(root, "recommendations", out var recommendations) ||
                    !TryReadStringArray(root, "priorityPlan", out var priorities))
                {
                    errorMessage = "WorkspaceStrategy.v1 requires a summary and non-empty string arrays for riskAnalysis, recommendations, and priorityPlan.";
                    return false;
                }

                var combined = string.Join(' ', new[] { summary }
                    .Concat(risks)
                    .Concat(recommendations)
                    .Concat(priorities));
                var placeholders = new[]
                {
                    "nhận định ngắn",
                    "rủi ro có căn cứ từ dữ liệu",
                    "hành động cụ thể người dùng có thể làm",
                    "tối đa 3 ưu tiên có thể thực hiện"
                };

                if (placeholders.Any(item => combined.Contains(item, StringComparison.OrdinalIgnoreCase)))
                {
                    errorMessage = "WorkspaceStrategy.v1 copied a schema placeholder instead of analysing the supplied metrics.";
                    return false;
                }

                var citedMetrics = Regex.Matches(combined, @"\d+(?:[.,]\d+)?")
                    .Select(match => match.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                if (citedMetrics < 2)
                {
                    errorMessage = "WorkspaceStrategy.v1 must cite at least two supplied numeric metrics.";
                    return false;
                }

                var vietnameseSignals = new[] { "dự án", "nhiệm vụ", "tiến độ", "cần ", "người dùng" };
                if (!vietnameseSignals.Any(item => combined.Contains(item, StringComparison.OrdinalIgnoreCase)))
                {
                    errorMessage = "WorkspaceStrategy.v1 must be written in Vietnamese.";
                    return false;
                }
            }
            else if (string.Equals(schemaId, "TextAnswer.v1", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "Root is not a JSON object.";
                    return false;
                }

                bool hasReply = root.TryGetProperty("reply", out _);
                bool hasMetrics = root.TryGetProperty("metrics", out var metrics) && metrics.ValueKind == JsonValueKind.Array;
                bool hasTables = root.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Array;
                bool hasCharts = root.TryGetProperty("charts", out var charts) && charts.ValueKind == JsonValueKind.Array;
                bool hasActions = root.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array;
                bool hasFiles = root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array;

                if (!hasReply || !hasMetrics || !hasTables || !hasCharts || !hasActions || !hasFiles)
                {
                    errorMessage = $"Missing required fields for TextAnswer.v1. HasReply={hasReply}, HasMetrics={hasMetrics}, HasTables={hasTables}, HasCharts={hasCharts}, HasActions={hasActions}, HasFiles={hasFiles}";
                    return false;
                }
            }
            else if (schemaId.Contains("acceptance_checklist", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "acceptance_checklist response must be a JSON object.";
                    return false;
                }
                if (!root.TryGetProperty("task_title", out var title) || title.ValueKind != JsonValueKind.String)
                {
                    errorMessage = "Missing or invalid 'task_title'.";
                    return false;
                }
                if (!root.TryGetProperty("checklist", out var checklist) || checklist.ValueKind != JsonValueKind.Array || checklist.GetArrayLength() < 1)
                {
                    errorMessage = "Missing, invalid or empty 'checklist' array.";
                    return false;
                }
                foreach (var item in checklist.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        errorMessage = "Checklist item must be a JSON object.";
                        return false;
                    }
                    if (!item.TryGetProperty("item", out var name) || name.ValueKind != JsonValueKind.String)
                    {
                        errorMessage = "Checklist item is missing required 'item' text.";
                        return false;
                    }
                    if (!item.TryGetProperty("type", out var typeVal) || typeVal.ValueKind != JsonValueKind.String)
                    {
                        errorMessage = "Checklist item is missing required 'type' string.";
                        return false;
                    }
                    if (!item.TryGetProperty("is_required", out var reqVal) || (reqVal.ValueKind != JsonValueKind.True && reqVal.ValueKind != JsonValueKind.False))
                    {
                        errorMessage = "Checklist item is missing required 'is_required' boolean.";
                        return false;
                    }
                }
            }
            else if (schemaId.Contains("assignee_recommendation", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "assignee_recommendation response must be a JSON object.";
                    return false;
                }
                if (!root.TryGetProperty("task_id", out _) || !root.TryGetProperty("method", out _) || !root.TryGetProperty("needs_human_confirm", out _))
                {
                    errorMessage = "Missing required fields task_id, method, or needs_human_confirm.";
                    return false;
                }
                if (!root.TryGetProperty("recommendations", out var recs) || recs.ValueKind != JsonValueKind.Array || recs.GetArrayLength() < 1)
                {
                    errorMessage = "Missing, invalid or empty 'recommendations' array.";
                    return false;
                }
                foreach (var rec in recs.EnumerateArray())
                {
                    if (rec.ValueKind != JsonValueKind.Object)
                    {
                        errorMessage = "Recommendation item must be a JSON object.";
                        return false;
                    }
                    if (!rec.TryGetProperty("member_id", out _) || !rec.TryGetProperty("member_name", out _) || !rec.TryGetProperty("score", out _) || !rec.TryGetProperty("reasons", out _) || !rec.TryGetProperty("risks", out _))
                    {
                        errorMessage = "Recommendation item is missing member_id, member_name, score, reasons, or risks.";
                        return false;
                    }
                }
            }
            else if (schemaId.Contains("chat_summary", StringComparison.OrdinalIgnoreCase) || schemaId.Contains("DiscussionSummary", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "chat_summary response must be a JSON object.";
                    return false;
                }
                if (schemaId.Contains("chat_summary", StringComparison.OrdinalIgnoreCase))
                {
                    if (!root.TryGetProperty("room_id", out _) || !root.TryGetProperty("message_range", out _) || !root.TryGetProperty("summary", out _) || !root.TryGetProperty("key_points", out _) || !root.TryGetProperty("open_questions", out _) || !root.TryGetProperty("action_candidates", out _))
                    {
                        errorMessage = "Missing required fields room_id, message_range, summary, key_points, open_questions, or action_candidates.";
                        return false;
                    }
                }
                else
                {
                    // Fallback to legacy DiscussionSummary
                    bool hasSummary = root.TryGetProperty("summary", out _);
                    bool hasDecisions = root.TryGetProperty("keyDecisions", out var decs) && decs.ValueKind == JsonValueKind.Array;
                    bool hasQuestions = root.TryGetProperty("unresolvedQuestions", out var quests) && quests.ValueKind == JsonValueKind.Array;

                    if (!hasSummary || !hasDecisions || !hasQuestions)
                    {
                        errorMessage = $"Missing required fields for DiscussionSummary. HasSummary={hasSummary}, HasDecisions={hasDecisions}, HasQuestions={hasQuestions}";
                        return false;
                    }
                }
            }
            else if (schemaId.Contains("meeting_action_extract", StringComparison.OrdinalIgnoreCase) || schemaId.Contains("ActionItem", StringComparison.OrdinalIgnoreCase))
            {
                if (schemaId.Contains("meeting_action_extract", StringComparison.OrdinalIgnoreCase))
                {
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        errorMessage = "meeting_action_extract response must be a JSON object.";
                        return false;
                    }
                    if (!root.TryGetProperty("meeting", out var meeting) || meeting.ValueKind != JsonValueKind.Object || !meeting.TryGetProperty("title", out _) || !meeting.TryGetProperty("language", out _))
                    {
                        errorMessage = "Missing or invalid 'meeting' info.";
                        return false;
                    }
                    if (!root.TryGetProperty("summary", out _) || !root.TryGetProperty("keywords", out _) || !root.TryGetProperty("action_items", out var actionItems) || !root.TryGetProperty("decisions", out _) || !root.TryGetProperty("risks", out _) || !root.TryGetProperty("confidence", out _))
                    {
                        errorMessage = "Missing required fields summary, keywords, action_items, decisions, risks, or confidence.";
                        return false;
                    }
                    foreach (var item in actionItems.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            errorMessage = "ActionItem item must be a JSON object.";
                            return false;
                        }
                        if (!item.TryGetProperty("task", out _) || !item.TryGetProperty("assignee", out _) || !item.TryGetProperty("deadline", out _) || !item.TryGetProperty("confidence", out _) || !item.TryGetProperty("source_quote", out _))
                        {
                            errorMessage = "ActionItem item is missing task, assignee, deadline, confidence, or source_quote.";
                            return false;
                        }
                    }
                }
                else
                {
                    // Fallback to legacy ActionItem
                    if (root.ValueKind != JsonValueKind.Array)
                    {
                        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                        {
                            // Valid, items array present
                        }
                        else
                        {
                            errorMessage = "ActionItems response must be a JSON array or a JSON object containing an 'items' array.";
                            return false;
                        }
                    }
                }
            }
            else if (schemaId.Contains("privacy_data_request", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "privacy_data_request response must be a JSON object.";
                    return false;
                }
                if (!root.TryGetProperty("request_type", out _) || !root.TryGetProperty("requester_user_id", out _) || !root.TryGetProperty("scope", out _) || !root.TryGetProperty("status", out _))
                {
                    errorMessage = "Missing required fields request_type, requester_user_id, scope, or status.";
                    return false;
                }
            }
            else if (schemaId.Contains("progress_summary", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "progress_summary response must be a JSON object.";
                    return false;
                }
                if (!root.TryGetProperty("project_id", out _) || !root.TryGetProperty("period", out _) || !root.TryGetProperty("summary", out _) || !root.TryGetProperty("metrics", out var metrics) || !root.TryGetProperty("risks", out _) || !root.TryGetProperty("next_actions", out _))
                {
                    errorMessage = "Missing required fields project_id, period, summary, metrics, risks, or next_actions.";
                    return false;
                }
                if (metrics.ValueKind != JsonValueKind.Object || !metrics.TryGetProperty("done", out _) || !metrics.TryGetProperty("in_progress", out _) || !metrics.TryGetProperty("todo", out _) || !metrics.TryGetProperty("overdue", out _))
                {
                    errorMessage = "Metrics object is missing done, in_progress, todo, or overdue count.";
                    return false;
                }
            }
            else if (schemaId.Contains("task_breakdown", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "task_breakdown response must be a JSON object.";
                    return false;
                }
                if (!root.TryGetProperty("parent_task", out _) || !root.TryGetProperty("subtasks", out var subtasks) || subtasks.ValueKind != JsonValueKind.Array || subtasks.GetArrayLength() < 1)
                {
                    errorMessage = "Missing parent_task or missing/empty subtasks array.";
                    return false;
                }
                foreach (var st in subtasks.EnumerateArray())
                {
                    if (st.ValueKind != JsonValueKind.Object)
                    {
                        errorMessage = "Subtask item must be a JSON object.";
                        return false;
                    }
                    if (!st.TryGetProperty("title", out _) || !st.TryGetProperty("description", out _) || !st.TryGetProperty("estimated_effort", out _) || !st.TryGetProperty("order", out _))
                    {
                        errorMessage = "Subtask is missing title, description, estimated_effort, or order.";
                        return false;
                    }
                }
            }
            else if (schemaId.Contains("task_draft", StringComparison.OrdinalIgnoreCase) || schemaId.Contains("ProjectDraft", StringComparison.OrdinalIgnoreCase) || schemaId.Contains("DraftProject", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "ProjectDraft / task_draft response must be a JSON object.";
                    return false;
                }
                if (schemaId.Contains("task_draft", StringComparison.OrdinalIgnoreCase))
                {
                    if (!root.TryGetProperty("title", out _) || !root.TryGetProperty("description", out _) || !root.TryGetProperty("priority", out _) || !root.TryGetProperty("deadline", out _) || !root.TryGetProperty("assignee_suggestion", out _) || !root.TryGetProperty("source_refs", out _) || !root.TryGetProperty("confidence", out _))
                    {
                        errorMessage = "Missing required fields title, description, priority, deadline, assignee_suggestion, source_refs, or confidence.";
                        return false;
                    }
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"Invalid JSON format: {ex.Message}";
            return false;
        }
    }

    private static bool TryReadNonEmptyString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? string.Empty;
        return value.Length >= 12;
    }

    private static bool TryReadStringArray(JsonElement root, string propertyName, out IReadOnlyList<string> values)
    {
        values = [];
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array ||
            property.GetArrayLength() == 0)
        {
            return false;
        }

        var parsed = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                return false;
            }

            parsed.Add(item.GetString()!.Trim());
        }

        values = parsed;
        return true;
    }
}
