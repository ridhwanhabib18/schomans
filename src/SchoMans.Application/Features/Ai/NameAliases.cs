using System.Text.RegularExpressions;

namespace SchoMans.Application.Features.Ai;

/// <summary>
/// Keeps the names of students out of what leaves for a provider (PRD F-10): a student is known to the model as <c>S1</c>,
/// <c>S2</c>, and so on. Teacher notes are free text and may contain a name, so <see cref="Scrub"/> swaps every known name in
/// a text for the alias, and <see cref="Restore"/> puts the real names back in the answer. The map lives only in memory.
/// </summary>
public sealed partial class NameAliases
{
    private readonly Dictionary<Guid, string> _aliases = [];
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly List<(Regex Pattern, string Alias)> _scrubbers = [];

    /// <summary>The alias of a student; the first call for a student gives them the next number.</summary>
    public string For(Guid studentId, string fullName)
    {
        if (_aliases.TryGetValue(studentId, out var known))
        {
            return known;
        }

        var alias = $"S{_aliases.Count + 1}";
        _aliases[studentId] = alias;
        _names[alias] = fullName;

        // The full name first, then each part of it that is long enough not to hit ordinary words.
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length >= 3)
            .Prepend(fullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(p => p.Length);
        foreach (var part in parts)
        {
            _scrubbers.Add((new Regex($@"\b{Regex.Escape(part)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)), alias));
        }

        // Longest names must win over their parts, so keep the list ordered by pattern length.
        _scrubbers.Sort((a, b) => b.Pattern.ToString().Length.CompareTo(a.Pattern.ToString().Length));
        return alias;
    }

    /// <summary>Replaces every known name (and part of a name) in a text with the alias of its student.</summary>
    public string Scrub(string text)
    {
        foreach (var (pattern, alias) in _scrubbers)
        {
            text = pattern.Replace(text, alias);
        }

        return text;
    }

    /// <summary>Replaces the aliases in an answer with the real names.</summary>
    public string Restore(string text) => AliasPattern().Replace(text, match => _names.TryGetValue(match.Value, out var name) ? name : match.Value);

    [GeneratedRegex(@"\bS\d+\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex AliasPattern();
}
