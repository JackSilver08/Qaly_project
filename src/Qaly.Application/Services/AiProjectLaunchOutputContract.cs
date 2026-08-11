using System.Text.Json;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public sealed record AiProjectLaunchModelOutput(
    string ProposedProjectName,
    string Objective,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> SuccessMeasures,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Unknowns);

public static class AiProjectLaunchOutputContract
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParse(string content, out AiProjectLaunchModelOutput? output, out string? error)
    {
        output = null;
        error = null;
        try
        {
            var candidate = JsonSerializer.Deserialize<AiProjectLaunchModelOutput>(content, JsonOptions);
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.ProposedProjectName) ||
                string.IsNullOrWhiteSpace(candidate.Objective))
            {
                error = "Project launch output requires proposedProjectName and objective.";
                return false;
            }

            var normalized = candidate with
            {
                ProposedProjectName = Limit(candidate.ProposedProjectName, 160),
                Objective = Limit(candidate.Objective, 1200),
                Scope = Normalize(candidate.Scope, 12),
                Exclusions = Normalize(candidate.Exclusions, 8),
                SuccessMeasures = Normalize(candidate.SuccessMeasures, 8),
                Facts = Normalize(candidate.Facts, 10),
                Assumptions = Normalize(candidate.Assumptions, 10),
                Unknowns = Normalize(candidate.Unknowns, 10)
            };
            if (normalized.Scope.Count == 0 || normalized.SuccessMeasures.Count == 0)
            {
                error = "Project launch output requires non-empty scope and successMeasures.";
                return false;
            }

            output = normalized;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string[] Normalize(IReadOnlyList<string>? values, int max)
        => values?.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => Limit(item, 500))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToArray() ?? [];

    private static string Limit(string value, int max)
    {
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
