using System.Text.Json;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.Infrastructure.Services.AI;

public static class TaskSkillSuggestionContract
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool SnapshotIsEmpty(string snapshotJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<TaskSkillSuggestionSnapshotDto>(snapshotJson, JsonOptions);
            return snapshot == null || snapshot.Skills == null || snapshot.Skills.Count == 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryBuildEmptyResult(
        string snapshotJson,
        out string resultJson,
        out string? error)
    {
        resultJson = string.Empty;
        if (!SchemaArtifactIsAvailable(out error) ||
            !TryReadSnapshot(snapshotJson, out var snapshot, out error))
        {
            return false;
        }

        resultJson = JsonSerializer.Serialize(new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            snapshot!.Task.Id,
            snapshot.SourceVersion,
            "empty",
            [],
            [],
            DateTimeOffset.UtcNow), JsonOptions);
        return true;
    }

    public static bool TryBuildResult(
        string modelJson,
        string snapshotJson,
        out string resultJson,
        out string? error)
    {
        resultJson = string.Empty;
        if (!SchemaArtifactIsAvailable(out error) ||
            !TryReadSnapshot(snapshotJson, out var snapshot, out error))
        {
            return false;
        }

        TaskSkillSuggestionOutputDto? model;
        try
        {
            model = JsonSerializer.Deserialize<TaskSkillSuggestionOutputDto>(modelJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Task skill response is invalid JSON: {exception.Message}";
            return false;
        }

        if (model == null)
        {
            error = "Task skill response is empty.";
            return false;
        }
        if (!string.Equals(model.SchemaId, TaskSkillAiContract.SchemaId, StringComparison.Ordinal))
        {
            error = $"schemaId must equal {TaskSkillAiContract.SchemaId}.";
            return false;
        }
        if (model.TaskId != snapshot!.Task.Id)
        {
            error = "taskId does not match the authorized task.";
            return false;
        }
        if (!string.Equals(model.SourceVersion, snapshot.SourceVersion, StringComparison.Ordinal))
        {
            error = "sourceVersion does not match the authorized source snapshot.";
            return false;
        }

        var dataState = model.DataState?.Trim().ToLowerInvariant();
        if (dataState is not ("ready" or "empty"))
        {
            error = "dataState must be ready or empty.";
            return false;
        }

        var catalog = snapshot.Skills.ToDictionary(skill => skill.Id);
        var suggestions = model.Suggestions ?? [];
        if (suggestions.Count > 10)
        {
            error = "suggestions cannot contain more than 10 items.";
            return false;
        }
        if (suggestions.Select(item => item.SkillId).Distinct().Count() != suggestions.Count)
        {
            error = "suggestions cannot contain duplicate skill IDs.";
            return false;
        }
        if (dataState == "empty" && suggestions.Count != 0)
        {
            error = "dataState=empty cannot include suggestions.";
            return false;
        }

        var reconciled = new List<TaskSkillSuggestionItemDto>(suggestions.Count);
        foreach (var suggestion in suggestions)
        {
            if (!catalog.TryGetValue(suggestion.SkillId, out var skill))
            {
                error = "A suggested skill is inactive, absent, or outside the authorized organization catalog.";
                return false;
            }
            if (!TaskSkillService.TryNormalizeLevel(suggestion.RequiredLevel, out var level))
            {
                error = "requiredLevel must be Familiar, Proficient, or Expert.";
                return false;
            }
            if (suggestion.Confidence is < 0m or > 1m)
            {
                error = "confidence must be between 0 and 1.";
                return false;
            }

            var rationale = suggestion.Rationale?.Trim();
            if (string.IsNullOrWhiteSpace(rationale) || rationale.Length > 500)
            {
                error = "Every suggestion requires a rationale of at most 500 characters.";
                return false;
            }

            var refs = suggestion.SourceRefs?
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => reference.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? [];
            if (refs.Count == 0 ||
                refs.Any(reference => !string.Equals(reference, snapshot.Task.SourceRef, StringComparison.Ordinal)))
            {
                error = "Every suggestion must cite only the authorized task source.";
                return false;
            }

            reconciled.Add(new TaskSkillSuggestionItemDto(
                skill.Id,
                skill.Name,
                level,
                Math.Round(suggestion.Confidence, 2, MidpointRounding.AwayFromZero),
                rationale,
                [snapshot.Task.SourceRef]));
        }

        var unmappedTerms = (model.UnmappedTerms ?? [])
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Where(term => term.Length <= 100)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        var finalState = reconciled.Count == 0 ? "empty" : "ready";
        var final = new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            snapshot.Task.Id,
            snapshot.SourceVersion,
            finalState,
            reconciled,
            unmappedTerms,
            DateTimeOffset.UtcNow);
        resultJson = JsonSerializer.Serialize(final, JsonOptions);
        error = null;
        return true;
    }

    public static bool TryValidateFinal(string resultJson, out string? error)
    {
        error = null;
        if (!SchemaArtifactIsAvailable(out error))
        {
            return false;
        }

        TaskSkillSuggestionOutputDto? result;
        try
        {
            result = JsonSerializer.Deserialize<TaskSkillSuggestionOutputDto>(resultJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Task skill result is invalid JSON: {exception.Message}";
            return false;
        }

        if (result == null ||
            !string.Equals(result.SchemaId, TaskSkillAiContract.SchemaId, StringComparison.Ordinal) ||
            result.TaskId == Guid.Empty ||
            string.IsNullOrWhiteSpace(result.SourceVersion) ||
            result.GeneratedAt == default)
        {
            error = "Task skill result is missing schemaId, taskId, sourceVersion, or generatedAt.";
            return false;
        }
        if (result.Suggestions == null ||
            result.UnmappedTerms == null ||
            result.DataState is not ("ready" or "empty") ||
            result.Suggestions.Count > 10 ||
            result.Suggestions.Select(item => item.SkillId).Distinct().Count() != result.Suggestions.Count ||
            result.DataState != (result.Suggestions.Count == 0 ? "empty" : "ready") ||
            result.UnmappedTerms.Count > 20 ||
            result.UnmappedTerms.Any(term => string.IsNullOrWhiteSpace(term) || term.Length > 100) ||
            result.UnmappedTerms.Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.UnmappedTerms.Count)
        {
            error = "Task skill result has an invalid dataState or suggestion count.";
            return false;
        }

        foreach (var suggestion in result.Suggestions)
        {
            var canonicalName = suggestion.CanonicalName?.Trim();
            var rationale = suggestion.Rationale?.Trim();
            var sourceRefs = suggestion.SourceRefs;
            if (suggestion.SkillId == Guid.Empty ||
                string.IsNullOrWhiteSpace(canonicalName) ||
                canonicalName.Length > 100 ||
                !TaskSkillService.TryNormalizeLevel(suggestion.RequiredLevel, out var normalizedLevel) ||
                !string.Equals(suggestion.RequiredLevel, normalizedLevel, StringComparison.Ordinal) ||
                suggestion.Confidence is < 0m or > 1m ||
                string.IsNullOrWhiteSpace(rationale) ||
                rationale.Length > 500 ||
                sourceRefs == null ||
                sourceRefs.Count is < 1 or > 8 ||
                sourceRefs.Any(reference => string.IsNullOrWhiteSpace(reference) || reference.Length > 200) ||
                sourceRefs.Distinct(StringComparer.Ordinal).Count() != sourceRefs.Count)
            {
                error = "Task skill result contains an invalid suggestion.";
                return false;
            }
        }

        return true;
    }

    private static bool SchemaArtifactIsAvailable(out string? error)
    {
        var outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "Schemas",
            "ai",
            "task_skill_suggestion.schema.json");
        var workspacePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "docs",
            "schemas",
            "ai",
            "task_skill_suggestion.schema.json");
        var path = File.Exists(outputPath) ? outputPath : workspacePath;
        if (!File.Exists(path))
        {
            error = "task_skill_suggestion.v1 schema artifact is missing.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("$id", out var id) ||
                !string.Equals(id.GetString(), TaskSkillAiContract.SchemaId, StringComparison.Ordinal))
            {
                error = "task_skill_suggestion schema artifact has the wrong $id.";
                return false;
            }
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or UnauthorizedAccessException)
        {
            error = $"task_skill_suggestion schema artifact cannot be read: {exception.Message}";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadSnapshot(
        string snapshotJson,
        out TaskSkillSuggestionSnapshotDto? snapshot,
        out string? error)
    {
        snapshot = null;
        error = null;
        try
        {
            snapshot = JsonSerializer.Deserialize<TaskSkillSuggestionSnapshotDto>(snapshotJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Task skill snapshot is invalid JSON: {exception.Message}";
            return false;
        }

        if (snapshot == null ||
            !string.Equals(snapshot.SchemaId, TaskSkillAiContract.SnapshotSchemaId, StringComparison.Ordinal) ||
            snapshot.Task == null ||
            snapshot.Skills == null ||
            snapshot.Task.Id == Guid.Empty ||
            snapshot.Task.ProjectId == Guid.Empty ||
            snapshot.Task.OrganizationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(snapshot.Task.Title) ||
            string.IsNullOrWhiteSpace(snapshot.Task.SourceRef) ||
            string.IsNullOrWhiteSpace(snapshot.SourceVersion) ||
            string.IsNullOrWhiteSpace(snapshot.CatalogVersion) ||
            snapshot.Skills.Any(skill => skill.Id == Guid.Empty || string.IsNullOrWhiteSpace(skill.Name)) ||
            snapshot.Skills.Select(skill => skill.Id).Distinct().Count() != snapshot.Skills.Count)
        {
            error = "Task skill snapshot is missing an authoritative task, source version, or canonical catalog.";
            return false;
        }

        return true;
    }
}
