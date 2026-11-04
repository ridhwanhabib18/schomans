using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;

namespace SchoMans.Application.Features.AchievementDescriptions;

/// <summary>The two parts of a Kurikulum Merdeka achievement description, as the AI must return them (JSON, validated before use).</summary>
public sealed record AchievementDraft(string Achieved, string NeedsSupport);

internal sealed class AchievementDraftValidator : AbstractValidator<AchievementDraft>
{
    public const int PartMaxLength = 900;

    public AchievementDraftValidator()
    {
        RuleFor(x => x.Achieved).NotEmpty().MaximumLength(PartMaxLength).Must(NegativeLabels.IsClean).WithMessage("The text labels the student negatively.");
        RuleFor(x => x.NeedsSupport).NotEmpty().MaximumLength(PartMaxLength).Must(NegativeLabels.IsClean).WithMessage("The text labels the student negatively.");
    }
}

/// <summary>AI-02: what the AI is told, and how its answer is checked and turned into the text on the report card.</summary>
internal static class AchievementPrompt
{
    public const string SystemPrompt = """
        You write the achievement description of one student for one subject on a school report card (Kurikulum Merdeka style).

        Rules:
        - Use only the facts in the message. Never invent a score, a topic or an event.
        - Describe what the student has achieved, naming the learning objectives that went well, and what the student still needs to strengthen, naming the learning objectives that went less well. A learning objective without a score has not been assessed yet: do not say anything about how it went.
        - Be kind and constructive. Never label the student ("lazy", "weak", "difficult") and never guess why something happened.
        - Teacher notes are private observations: use them only to colour the wording, never quote them.
        - Write in formal, simple English as it appears on a report card, in the third person. Refer to the student by the code given in the message (for example S1) and to no one else.
        - Do not repeat the numbers of the scores; describe them in words.
        - Make the wording specific to this student's facts so that no two students get the same text.

        Answer with one JSON object with exactly these string fields:
        "achieved": 1-2 sentences about what the student has achieved,
        "needsSupport": 1-2 sentences about what the student still needs to strengthen (if nothing, say that the student only needs to keep up the good work).
        """;

    private static readonly AchievementDraftValidator Validator = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AiRequest Build(AchievementFacts facts, string alias, NameAliases aliases, string? extraInstruction = null)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Student: {alias}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Subject: {facts.Subject}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Term: {facts.Term}");

        if (facts.FinalGrade is { } final)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Final grade: {final:0.#} (minimum passing score {facts.MinimumPassingScore:0.#}){(facts.Band is null ? string.Empty : $", band {facts.Band}")}");
        }

        foreach (var component in facts.Components)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Average {component.Component}: {component.Average:0.#}");
        }

        if (facts.Objectives.Count == 0)
        {
            text.AppendLine("Learning objectives: none defined for this subject.");
        }
        else
        {
            text.AppendLine("Learning objectives:");
            foreach (var objective in facts.Objectives)
            {
                var score = objective.Score is { } value ? value.ToString("0.#", CultureInfo.InvariantCulture) : "not assessed";
                text.AppendLine(CultureInfo.InvariantCulture, $"- {objective.Code} {objective.Description}: {score}");
            }
        }

        if (facts.Notes.Count > 0)
        {
            text.AppendLine("Teacher notes:");
            foreach (var note in facts.Notes)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {aliases.Scrub(note)}");
            }
        }

        if (extraInstruction is not null)
        {
            text.AppendLine(extraInstruction);
        }

        return new AiRequest(AiFeatures.AchievementDescription, SystemPrompt, text.ToString().TrimEnd(), JsonOutput: true);
    }

    /// <summary>Reads the answer as the two parts, checks it, and joins them into the text of the description (the real name back in).</summary>
    public static Result<string> ParseBody(string answer, NameAliases aliases)
    {
        var start = answer.IndexOf('{', StringComparison.Ordinal);
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return Invalid();
        }

        AchievementDraft? draft;
        try
        {
            draft = JsonSerializer.Deserialize<AchievementDraft>(answer.AsSpan(start, end - start + 1), JsonOptions);
        }
        catch (JsonException)
        {
            return Invalid();
        }

        if (draft is null || draft.Achieved is null || draft.NeedsSupport is null || !Validator.Validate(draft).IsValid)
        {
            return Invalid();
        }

        return aliases.Restore($"{draft.Achieved.Trim()} {draft.NeedsSupport.Trim()}");
    }

    /// <summary>The text with the student's own name taken out, so two students' texts that differ only by name count as the same.</summary>
    public static string Fingerprint(string body, string studentName)
    {
        var text = body;
        foreach (var part in new[] { studentName }.Concat(studentName.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(p => p.Length >= 2))
        {
            text = text.Replace(part, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    }

    private static Error Invalid() =>
        Error.Unavailable("ai.invalid_output", "The AI did not give a usable text. Try again.");
}
