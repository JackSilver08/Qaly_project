using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Qaly.Application.DTOs.Ai;

/// <summary>Shared lexical rules for routing only; never rewrites the user's payload or IDs.</summary>
public static class AiPromptLanguage
{
    public static string Normalize(string? value)
    {
        // These words have opposite meanings after accent folding (dùng = use, dừng = stop).
        var text = Regex.Replace((value ?? string.Empty).Normalize(NormalizationForm.FormC),
            @"\bdừng\b", "ngung", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        text = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('đ', 'd');
        // Accept emphatic spelling without ever collapsing counts (e.g. 111 tasks).
        text = Regex.Replace(text, @"(\p{L})\1{2,}", "$1");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    public static bool ContainsAny(string normalized, params string[] phrases)
        => phrases.Any(phrase => PhraseIndexes(normalized, phrase).Any());

    public static IEnumerable<int> PhraseIndexes(string normalized, string phrase)
    {
        for (var start = 0; start < normalized.Length;)
        {
            var index = normalized.IndexOf(phrase, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0) yield break;
            var end = index + phrase.Length;
            if ((index == 0 || !IsWordCharacter(normalized[index - 1])) &&
                (end == normalized.Length || !IsWordCharacter(normalized[end])))
                yield return index;
            start = end;
        }
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';
}
