using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Application.Features.PrincipalAssistant;

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
            AiFeatures.AchievementDescription => MockAchievementDescription(request.UserPrompt),
            AiFeatures.DocumentExtraction => MockDocumentExtraction(request),
            _ => request.JsonOutput ? "{\"mock\":true}" : "This is an answer of the mock AI.",
        };

        // The mock "reads" the prompt so its token counts move with the input, like a real provider's would.
        var tokens = (request.SystemPrompt.Length + request.UserPrompt.Length + text.Length) / 4;
        return Task.FromResult<Result<AiCompletion>>(new AiCompletion(text, Provider, "mock", tokens, 0m));
    }

    /// <summary>
    /// AI-04: the mock "understands" a question by its keywords and asks for the matching functions; once their results are in, it
    /// answers with their summaries. A real model decides this itself; the use case behaves the same either way.
    /// </summary>
    public Task<Result<AiChatReply>> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        var last = request.Turns.LastOrDefault();
        var calls = new List<AiToolCall>();
        string text;
        if (last?.Role == "tool")
        {
            var summaries = (last.ToolResults ?? []).Select(r => Summary(r.Content)).Where(t => t.Length > 0).ToList();
            text = summaries.Count == 0 ? "I could not find an answer in the data." : "Here is what I found. " + string.Join(" ", summaries);
        }
        else
        {
            var question = (last?.Text ?? string.Empty).ToLowerInvariant();
            var classroom = Regex.Match(question, @"\bclass(?:room)?\s+([a-z0-9-]+)");
            var args = classroom.Success ? JsonSerializer.Serialize(new { classroom = classroom.Groups[1].Value }) : "{}";

            void Ask(string name, string arguments) => calls.Add(new AiToolCall($"mock-call-{calls.Count + 1}", name, arguments));
            if (Regex.IsMatch(question, "trend|weekly|per week"))
            {
                Ask(AssistantToolNames.AttendanceTrend, args);
            }
            else if (Regex.IsMatch(question, "low attendance|at risk|below|worried|struggling"))
            {
                Ask(AssistantToolNames.StudentsLowAttendance, args);
            }
            else if (Regex.IsMatch(question, "attendance|absent|absence|present"))
            {
                Ask(AssistantToolNames.AttendanceByClassroom, args);
            }

            if (Regex.IsMatch(question, "grade|score|average|subject"))
            {
                Ask(AssistantToolNames.AverageGradesBySubject, args);
            }

            if (Regex.IsMatch(question, "request|approval|pending|leave|letter"))
            {
                Ask(AssistantToolNames.PendingRequests, "{}");
            }

            if (Regex.IsMatch(question, "applicant|admission|ppdb|enrol"))
            {
                Ask(AssistantToolNames.AdmissionApplicants, "{}");
            }

            if (Regex.IsMatch(question, "assignment|homework|submission|hand(?:ed)? in"))
            {
                Ask(AssistantToolNames.AssignmentSummary, args);
            }

            if (calls.Count == 0 && Regex.IsMatch(question, "classrooms|classes|how many students"))
            {
                Ask(AssistantToolNames.ListClassrooms, "{}");
            }

            text = calls.Count == 0
                ? "I can answer questions about attendance, grades, pending requests, admissions, assignments and the classrooms of the school."
                : string.Empty;
        }

        var tokens = (request.SystemPrompt.Length + request.Turns.Sum(t => t.Text.Length + (t.ToolResults?.Sum(r => r.Content.Length) ?? 0)) + text.Length) / 4;
        return Task.FromResult<Result<AiChatReply>>(new AiChatReply(text, calls, Provider, "mock", tokens, 0m));
    }

    private static string Summary(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            foreach (var name in new[] { "summary", "error" })
            {
                if (document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
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

    private static readonly string[] AchievedOpeners =
    [
        "{0} has shown a good grasp of {1}.",
        "{0} reached the expected level in {1}.",
        "In {1}, {0} met the goals with growing confidence.",
        "{0} worked steadily and did well in {1}.",
    ];

    private static readonly string[] SupportOpeners =
    [
        "{0} will benefit from more practice in {1}.",
        "To strengthen {1}, {0} is encouraged to review the material regularly.",
        "{0} needs a little more support to master {1}.",
        "Further attention to {1} will help {0} move forward.",
    ];

    /// <summary>
    /// AI-03: the mock cannot read a photo, so it gives the same fictional child for every document, as a consistent family would
    /// have. A file whose name contains "mismatch" gets another date of birth, to show the flag for documents that disagree.
    /// </summary>
    private static string MockDocumentExtraction(AiRequest request)
    {
        var type = Regex.Match(request.UserPrompt, @"Document type: (birth certificate|family card|school report card)").Groups[1].Value;
        var mismatch = request.Images?.Any(i => i.Name.Contains("mismatch", StringComparison.OrdinalIgnoreCase)) == true;

        object? grades = type == "school report card"
            ? new[] { new { subject = "Mathematics", score = 82 }, new { subject = "English", score = 78 }, new { subject = "Natural Sciences", score = 85 } }
            : Array.Empty<object>();
        var fields = new
        {
            fullName = "Rina Putri Lestari",
            nik = type == "school report card" ? null : "3201014505140001",
            dateOfBirth = mismatch ? "2014-06-17" : "2014-05-05",
            placeOfBirth = "Bandung",
            address = type == "family card" ? "Jl. Merdeka No. 10, Bandung" : null,
            fatherName = type == "school report card" ? null : "Budi Santoso",
            motherName = type == "school report card" ? null : "Sari Lestari",
            guardianName = type == "family card" ? "Budi Santoso" : null,
            grades,
        };
        return JsonSerializer.Serialize(fields);
    }

    private static readonly string[] Closings =
    [
        "The teachers will keep following this closely.",
        "Steady practice at home will help.",
        "The next term is a good time to build on this.",
        "Regular review will make the difference.",
        "Encouragement from home will support this progress.",
    ];

    /// <summary>
    /// AI-02: a description built from the facts in the prompt: the objectives with the highest and the lowest score (or the subject
    /// itself when no objective was assessed). The wording is picked from a hash of the facts, so students with different scores get different texts.
    /// </summary>
    private static string MockAchievementDescription(string prompt)
    {
        var alias = Regex.Match(prompt, @"Student: (S\d+)").Groups[1].Value;
        if (alias.Length == 0)
        {
            alias = "S1";
        }

        var subject = Regex.Match(prompt, @"Subject: (.+)").Groups[1].Value.Trim();
        var assessed = Regex.Matches(prompt, @"^- (\S+) (.+): (\d+(?:\.\d+)?)$", RegexOptions.Multiline)
            .Select(m => (Code: m.Groups[1].Value, Name: m.Groups[2].Value, Score: decimal.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)))
            .ToList();

        // The wording is a combination (opener, support, closing) picked by a hash of the facts. A request for "variation n" moves the
        // pick on by n, so an answer asked for again never repeats a combination a classmate got.
        var variation = Number(prompt, @"\(variation (\d+)\)");
        var facts = Regex.Replace(prompt, @"^Note: your previous text.*$", string.Empty, RegexOptions.Multiline);
        var pick = ((facts.Aggregate(0, (sum, c) => unchecked((sum * 31) + c)) & 0xFFFF) + variation) % (AchievedOpeners.Length * SupportOpeners.Length * Closings.Length);
        var best = assessed.Count == 0 ? subject : assessed.MaxBy(a => a.Score).Name;
        var weakest = assessed.Count < 2 ? subject : assessed.MinBy(a => a.Score).Name;

        var final = Regex.Match(prompt, @"Final grade: (\d+(?:\.\d+)?)").Groups[1].Value;
        var achieved = string.Format(CultureInfo.InvariantCulture, AchievedOpeners[pick % AchievedOpeners.Length], alias, best.ToLowerInvariant())
            + (final.Length == 0 ? string.Empty : $" The final grade for this term is {final}.");
        var needsSupport = assessed.Count >= 2 && assessed.Max(a => a.Score) - assessed.Min(a => a.Score) < 5
            ? $"{alias} only needs to keep up the good work."
            : string.Format(CultureInfo.InvariantCulture, SupportOpeners[(pick / AchievedOpeners.Length) % SupportOpeners.Length], alias, weakest.ToLowerInvariant());
        needsSupport += " " + Closings[pick / (AchievedOpeners.Length * SupportOpeners.Length)];

        return JsonSerializer.Serialize(new { achieved, needsSupport });
    }

    private static int Number(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }
}
