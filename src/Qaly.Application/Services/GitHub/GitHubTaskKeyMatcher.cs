using System.Globalization;
using System.Text.RegularExpressions;

namespace Qaly.Application.Services.GitHub;

public static class GitHubTaskKeyMatcher
{
    private static readonly RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static IReadOnlyList<int> ExtractNumbers(string? text, string projectCode)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(projectCode))
        {
            return [];
        }

        var pattern = $@"(?<![A-Z0-9]){Regex.Escape(projectCode)}-(?<number>\d+)(?!\d)";
        return Regex.Matches(text, pattern, Options)
            .Select(match => int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();
    }

    public static bool ContainsTaskKey(string? text, string taskKey)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(taskKey))
        {
            return false;
        }

        return text.Contains(taskKey, StringComparison.OrdinalIgnoreCase);
    }
}
