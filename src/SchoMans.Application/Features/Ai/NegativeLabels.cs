using System.Text.RegularExpressions;

namespace SchoMans.Application.Features.Ai;

/// <summary>Words a text about a child (a letter to parents, a report card) must never use. A draft with one of them is refused, not repaired.</summary>
internal static class NegativeLabels
{
    private static readonly Regex Pattern = new(
        @"\b(lazy|stupid|dumb|naughty|troublemaker|hopeless|problem child|bad student|malas|bodoh|nakal)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public static bool IsClean(string? text) => text is null || !Pattern.IsMatch(text);
}
