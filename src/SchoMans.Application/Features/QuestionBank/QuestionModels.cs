using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments.ListTeachingOptions;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.QuestionBank;

/// <summary>One question as a teacher writes it, or as the AI drafts it: the same shape in and out.</summary>
/// <param name="Options">Two to five for multiple choice, none for short answer.</param>
/// <param name="AnswerKey">For multiple choice the position (from 0) of the right option; for short answer the model answer.</param>
public sealed record QuestionInput(QuestionType Type, QuestionDifficulty Difficulty, string Body, List<string> Options, string AnswerKey, string Explanation);

public sealed record QuestionDto(
    Guid Id,
    Guid SubjectId,
    QuestionType Type,
    QuestionDifficulty Difficulty,
    string Body,
    List<string> Options,
    string AnswerKey,
    string Explanation,
    string CreatedByName,
    bool IsMine);

internal static class QuestionRules
{
    /// <summary>Why a question cannot be stored, or null. The same rules as the entity, so a bad question is a validation error and never an exception.</summary>
    public static string? Check(QuestionInput? q)
    {
        if (q is null || string.IsNullOrWhiteSpace(q.Body) || q.Body.Trim().Length > Question.BodyMaxLength)
        {
            return $"A question has a text of at most {Question.BodyMaxLength} characters.";
        }

        if (!Enum.IsDefined(q.Type) || !Enum.IsDefined(q.Difficulty))
        {
            return "The type or the difficulty is not valid.";
        }

        if (string.IsNullOrWhiteSpace(q.AnswerKey) || q.AnswerKey.Trim().Length > Question.AnswerKeyMaxLength)
        {
            return "A question has an answer key.";
        }

        if ((q.Explanation ?? string.Empty).Trim().Length > Question.ExplanationMaxLength)
        {
            return $"An explanation has at most {Question.ExplanationMaxLength} characters.";
        }

        var options = (q.Options ?? []).Select(o => o?.Trim() ?? string.Empty).ToList();
        if (q.Type == QuestionType.ShortAnswer)
        {
            return options.Count == 0 ? null : "A short answer question has no options.";
        }

        if (options.Count is < Question.MinOptions or > Question.MaxOptions || options.Any(o => o.Length is 0 or > Question.OptionMaxLength)
            || options.Select(o => o.ToUpperInvariant()).Distinct().Count() != options.Count)
        {
            return "A multiple choice question has 2 to 5 different options.";
        }

        return int.TryParse(q.AnswerKey.Trim(), out var position) && position >= 0 && position < options.Count
            ? null
            : "The answer key of a multiple choice question is the position of an option.";
    }

    public static Question ToEntity(Guid subjectId, Guid teacherId, QuestionInput q) =>
        Question.Create(subjectId, teacherId, q.Type, q.Difficulty, q.Body, q.Options ?? [], q.AnswerKey, q.Explanation ?? string.Empty);

    /// <summary>The teacher of the current user, when they teach the subject this year; the bank of a subject belongs to its teachers.</summary>
    public static async Task<Result<Guid>> TeacherOfSubjectAsync(IAppDbContext db, ICurrentUser user, Guid subjectId, CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, user, ct);
        if (teacherId is null)
        {
            return Error.Forbidden("questions.not_a_teacher", "Only teachers use the question bank.");
        }

        var taught = await ListTeachingOptionsHandler.TeachingPairs(db, teacherId.Value).AnyAsync(p => p.SubjectId == subjectId, ct);
        return taught ? teacherId.Value : Error.Forbidden("questions.not_your_subject", "You can only use the question bank of a subject you teach.");
    }
}
