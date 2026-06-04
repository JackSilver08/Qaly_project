#pragma warning disable CA1822 // Mark members as static

using System;
using System.Text.Json;

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

            if (string.Equals(schemaId, "TextAnswer.v1", StringComparison.OrdinalIgnoreCase))
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
            else if (schemaId.Contains("ActionItem", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Array)
                {
                    errorMessage = "ActionItems response must be a JSON array.";
                    return false;
                }
            }
            else if (schemaId.Contains("ProjectDraft", StringComparison.OrdinalIgnoreCase) || schemaId.Contains("DraftProject", StringComparison.OrdinalIgnoreCase))
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = "ProjectDraft response must be a JSON object.";
                    return false;
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
}
