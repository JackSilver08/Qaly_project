using System.Text.Json;

namespace Qaly.Application.Services;

public static class MeetingChecknoteAiContract
{
    public const string SchemaId = "meeting_checknote.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryValidateModel(
        string content,
        string? transcript,
        out MeetingChecknoteModelOutput? output,
        out string? errorMessage)
    {
        output = null;
        errorMessage = null;
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (!HasOnlyProperties(root, "summary", "summaryEvidence", "decisions", "risks", "actionItems") ||
                !TryReadText(root, "summary", 2_000, out _) ||
                !TryValidateEvidenceArray(root, "summaryEvidence", transcript, 1, 8, out errorMessage) ||
                !TryValidateGroundedItems(root, "decisions", ["text", "reason", "evidence"], transcript, 12, out errorMessage) ||
                !TryValidateGroundedItems(root, "risks", ["text", "severity", "evidence"], transcript, 12, out errorMessage) ||
                !TryValidateActions(root, transcript, out errorMessage))
            {
                errorMessage ??= "Meeting checknote output does not match meeting_checknote.v1.";
                return false;
            }

            foreach (var risk in root.GetProperty("risks").EnumerateArray())
            {
                if (risk.GetProperty("severity").GetString() is not ("low" or "medium" or "high"))
                {
                    errorMessage = "Meeting risk severity must be low, medium, or high.";
                    return false;
                }
            }

            output = JsonSerializer.Deserialize<MeetingChecknoteModelOutput>(
                content,
                JsonOptions);
            if (output == null)
            {
                errorMessage = "Meeting checknote output could not be deserialized.";
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            errorMessage = $"Invalid meeting checknote JSON: {exception.Message}";
            return false;
        }
    }

    public static bool TryLocateEvidence(string transcript, string evidence, out int start, out int end)
    {
        start = transcript.IndexOf(evidence, StringComparison.OrdinalIgnoreCase);
        end = start < 0 ? -1 : start + evidence.Length;
        return start >= 0;
    }

    private static bool TryValidateActions(JsonElement root, string? transcript, out string? errorMessage)
    {
        errorMessage = null;
        if (!root.TryGetProperty("actionItems", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 20)
        {
            errorMessage = "actionItems must be an array with at most 20 entries.";
            return false;
        }
        foreach (var item in items.EnumerateArray())
        {
            if (!HasOnlyProperties(item, "title", "description", "suggestedOwner", "dueDate", "priority", "evidence") ||
                !TryReadText(item, "title", 200, out _) ||
                !TryReadNullableText(item, "description", 2_000) ||
                !TryReadNullableText(item, "suggestedOwner", 200) ||
                !TryReadNullableDate(item, "dueDate") ||
                !TryReadText(item, "priority", 20, out var priority) ||
                priority is not ("Low" or "Medium" or "High" or "Critical") ||
                !TryReadText(item, "evidence", 800, out var evidence) ||
                !EvidenceExists(transcript, evidence!))
            {
                errorMessage = "An action item is malformed or not grounded in the transcript.";
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateEvidenceArray(
        JsonElement root,
        string property,
        string? transcript,
        int minimum,
        int maximum,
        out string? errorMessage)
    {
        errorMessage = null;
        if (!root.TryGetProperty(property, out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() < minimum || items.GetArrayLength() > maximum)
        {
            errorMessage = $"{property} must contain between {minimum} and {maximum} quotes.";
            return false;
        }
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()) || item.GetString()!.Length > 800 || !EvidenceExists(transcript, item.GetString()!))
            {
                errorMessage = $"{property} contains a quote not found in the transcript.";
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateGroundedItems(
        JsonElement root,
        string property,
        IReadOnlyList<string> expected,
        string? transcript,
        int maximum,
        out string? errorMessage)
    {
        errorMessage = null;
        if (!root.TryGetProperty(property, out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > maximum)
        {
            errorMessage = $"{property} must be a bounded array.";
            return false;
        }
        foreach (var item in items.EnumerateArray())
        {
            if (!HasOnlyProperties(item, expected.ToArray()) ||
                !TryReadText(item, "text", 800, out _) ||
                (expected.Contains("reason") && !TryReadNullableText(item, "reason", 800)) ||
                !TryReadText(item, "evidence", 800, out var evidence) ||
                !EvidenceExists(transcript, evidence!))
            {
                errorMessage = $"{property} contains an ungrounded item.";
                return false;
            }
        }
        return true;
    }

    private static bool EvidenceExists(string? transcript, string evidence)
        => !string.IsNullOrWhiteSpace(transcript) && transcript.Contains(evidence, StringComparison.OrdinalIgnoreCase);

    private static bool TryReadText(JsonElement root, string property, int maximum, out string? value)
    {
        value = root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
    }

    private static bool TryReadNullableText(JsonElement root, string property, int maximum)
        => root.TryGetProperty(property, out var element) &&
           (element.ValueKind == JsonValueKind.Null || (element.ValueKind == JsonValueKind.String && element.GetString()!.Length <= maximum));

    private static bool TryReadNullableDate(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) &&
           (element.ValueKind == JsonValueKind.Null ||
            (element.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(element.GetString(), out _)));

    private static bool HasOnlyProperties(JsonElement element, params string[] expected)
        => element.ValueKind == JsonValueKind.Object &&
           element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected);
}

public sealed record MeetingChecknoteModelOutput(
    string Summary,
    IReadOnlyList<string> SummaryEvidence,
    IReadOnlyList<MeetingChecknoteDecision> Decisions,
    IReadOnlyList<MeetingChecknoteRisk> Risks,
    IReadOnlyList<MeetingChecknoteAction> ActionItems);

public sealed record MeetingChecknoteDecision(string Text, string? Reason, string Evidence);
public sealed record MeetingChecknoteRisk(string Text, string Severity, string Evidence);
public sealed record MeetingChecknoteAction(
    string Title,
    string? Description,
    string? SuggestedOwner,
    string? DueDate,
    string Priority,
    string Evidence);
