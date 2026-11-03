using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;

namespace SchoMans.Infrastructure.Ai;

/// <summary>
/// The default provider (ADR-004): fixed, deterministic answers, so demos and tests need no key and cost nothing. Each feature
/// adds its own answer here, shaped like what the feature expects from a real model.
/// </summary>
public sealed class MockAiService : IAiService
{
    public string Provider => AiProviders.Mock;

    public Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var text = request.Feature switch
        {
            AiFeatures.ProgressSummary => MockProgressSummary(request.UserPrompt),
            _ => request.JsonOutput ? "{\"mock\":true}" : "This is an answer of the mock AI.",
        };

        // The mock "reads" the prompt so its token counts move with the input, like a real provider's would.
        var tokens = (request.SystemPrompt.Length + request.UserPrompt.Length + text.Length) / 4;
        return Task.FromResult<Result<AiCompletion>>(new AiCompletion(text, Provider, "mock", tokens, 0m));
    }

    private static readonly string[] HomeSuggestions =
    [
        "Ask {0} to tell you about one thing learned this week; explaining it aloud helps it stick.",
        "Set a short, quiet reading or homework time with {0} at the same hour each day.",
        "Look at {0}'s school bag together on Sunday evening so the week starts well prepared.",
    ];

    /// <summary>AI-01: a letter built from the facts in the prompt (absences, assignments not handed in, grades), in the JSON shape the use case expects.</summary>
    private static string MockProgressSummary(string prompt)
    {
        var alias = Regex.Match(prompt, @"Student: (S\d+)").Groups[1].Value;
        if (alias.Length == 0)
        {
            alias = "S1";
        }

        var absent = Number(prompt, @"absent (\d+)");
        var notHandedIn = Number(prompt, @"not handed in (\d+)");
        var hasGrades = prompt.Contains("Grades entered this month", StringComparison.Ordinal);
        var hasNotes = prompt.Contains("Teacher notes:", StringComparison.Ordinal);

        var needsAttention = (absent, notHandedIn) switch
        {
            (> 0, _) => $"{alias} missed {absent} lesson(s) this month; going over that material together would help.",
            (_, > 0) => $"{notHandedIn} assignment(s) were not handed in; a short plan for finishing them would help {alias}.",
            _ => "Nothing stands out this month; the work went steadily.",
        };

        var draft = new
        {
            paragraph = $"This letter is about {alias}'s month at school. {alias} took part in lessons and worked through the month's tasks."
                + (hasNotes ? " The teachers also shared a few observations, which are reflected below." : string.Empty),
            goingWell = hasGrades
                ? $"Grades were entered for {alias} this month and show steady effort."
                : $"{alias} kept taking part in class activities.",
            needsAttention,
            homeSuggestion = string.Format(CultureInfo.InvariantCulture, HomeSuggestions[(Number(alias, @"S(\d+)") + absent) % HomeSuggestions.Length], alias),
        };
        return JsonSerializer.Serialize(draft);
    }

    private static int Number(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }
}
