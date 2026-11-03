using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;

namespace SchoMans.Application.Features.ProgressSummaries;

/// <summary>The four parts of the letter, as the AI must return them (JSON, validated before use).</summary>
public sealed record ProgressSummaryDraft(string Paragraph, string GoingWell, string NeedsAttention, string HomeSuggestion);

internal sealed class ProgressSummaryDraftValidator : AbstractValidator<ProgressSummaryDraft>
{
    public const int ParagraphMaxLength = 900;
    public const int PointMaxLength = 600;

    // Words a letter to parents must never use about a child. A draft with one of them is refused, not repaired.
    private static readonly Regex NegativeLabels = new(
        @"\b(lazy|stupid|dumb|naughty|troublemaker|hopeless|problem child|bad student|malas|bodoh|nakal)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public ProgressSummaryDraftValidator()
    {
        RuleFor(x => x.Paragraph).NotEmpty().MaximumLength(ParagraphMaxLength).Must(NoNegativeLabel).WithMessage("The text labels the child negatively.");
        RuleFor(x => x.GoingWell).NotEmpty().MaximumLength(PointMaxLength).Must(NoNegativeLabel).WithMessage("The text labels the child negatively.");
        RuleFor(x => x.NeedsAttention).NotEmpty().MaximumLength(PointMaxLength).Must(NoNegativeLabel).WithMessage("The text labels the child negatively.");
        RuleFor(x => x.HomeSuggestion).NotEmpty().MaximumLength(PointMaxLength).Must(NoNegativeLabel).WithMessage("The text labels the child negatively.");
    }

    private static bool NoNegativeLabel(string? text) => text is null || !NegativeLabels.IsMatch(text);
}

/// <summary>AI-01: what the AI is told, and how its answer is checked and turned into the text of a summary.</summary>
internal static class ProgressSummaryPrompt
{
    public const string SystemPrompt = """
        You write the monthly progress letter that a school sends to the parents of one student.

        Rules:
        - Use only the facts in the message. Never invent a grade, a date, an event or a number.
        - Never guess why something happened and never label the child ("lazy", "weak", "difficult"). Describe what happened, in a kind and neutral way.
        - If a topic has no facts, say nothing about it instead of guessing.
        - Write simple, warm English that a parent without a school background understands. No jargon, no bullet symbols.
        - Refer to the child by the code given in the message (for example S1). Do not use any other name.
        - Teacher notes are private observations: turn them into gentle wording, never quote them word for word.

        Answer with one JSON object with exactly these string fields:
        "paragraph": one short paragraph about the month as a whole (at most 4 sentences),
        "goingWell": what is going well (1-2 sentences),
        "needsAttention": what needs attention (1-2 sentences; if nothing does, say that the month went steadily),
        "homeSuggestion": one concrete, friendly suggestion for the parents to do at home (1-2 sentences).
        """;

    private static readonly ProgressSummaryDraftValidator Validator = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AiRequest Build(ProgressFacts facts, string alias, NameAliases aliases)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Student: {alias}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Month: {facts.Month}");

        var recorded = facts.Present + facts.Excused + facts.Sick + facts.Absent;
        text.AppendLine(recorded == 0
            ? "Attendance: no lessons recorded."
            : $"Attendance: present {facts.Present}, excused {facts.Excused}, sick {facts.Sick}, absent {facts.Absent} (of {recorded} lessons).");

        if (facts.Grades.Count == 0)
        {
            text.AppendLine("Grades: none entered this month.");
        }
        else
        {
            text.AppendLine("Grades entered this month:");
            foreach (var grade in facts.Grades)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {grade.Subject}, {grade.Component}: {grade.Score:0.#}");
            }
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"Assignments: handed in {facts.Submitted} ({facts.SubmittedLate} of them late), not handed in {facts.NotHandedIn}.");
        if (facts.AverageSubmissionScore is { } average)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Average score of the scored assignments: {average:0.#}");
        }

        if (facts.Notes.Count > 0)
        {
            text.AppendLine("Teacher notes:");
            foreach (var note in facts.Notes)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {aliases.Scrub(note)}");
            }
        }

        return new AiRequest(AiFeatures.ProgressSummary, SystemPrompt, text.ToString().TrimEnd(), JsonOutput: true);
    }

    /// <summary>Reads the answer as the four parts, checks it, and joins them into the text of the summary (real names back in).</summary>
    public static Result<string> ParseBody(string answer, NameAliases aliases)
    {
        var start = answer.IndexOf('{', StringComparison.Ordinal);
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return Invalid();
        }

        ProgressSummaryDraft? draft;
        try
        {
            draft = JsonSerializer.Deserialize<ProgressSummaryDraft>(answer.AsSpan(start, end - start + 1), JsonOptions);
        }
        catch (JsonException)
        {
            return Invalid();
        }

        if (draft is null || draft.Paragraph is null || draft.GoingWell is null || draft.NeedsAttention is null || draft.HomeSuggestion is null
            || !Validator.Validate(draft).IsValid)
        {
            return Invalid();
        }

        var body = $"{draft.Paragraph.Trim()}\n\nWhat is going well: {draft.GoingWell.Trim()}\nWhat needs attention: {draft.NeedsAttention.Trim()}\nA suggestion for home: {draft.HomeSuggestion.Trim()}";
        return aliases.Restore(body);
    }

    private static Error Invalid() =>
        Error.Unavailable("ai.invalid_output", "The AI did not give a usable summary. Try again.");
}
