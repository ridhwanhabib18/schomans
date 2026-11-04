using System.Text.Json;
using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Assignments;

public enum QuestionType
{
    MultipleChoice,
    ShortAnswer,
}

public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard,
}

/// <summary>
/// A question of the question bank of a subject (AI-06). A multiple choice question has 2 to 5 options and the position (from 0) of the
/// right one as its answer key; a short answer question has no options and a model answer for the teacher. Written by a teacher,
/// by hand or after an AI draft that the teacher edited.
/// </summary>
public sealed class Question : BaseEntity
{
    public const int BodyMaxLength = 1_000;
    public const int OptionMaxLength = 300;
    public const int AnswerKeyMaxLength = 500;
    public const int ExplanationMaxLength = 500;
    public const int MinOptions = 2;
    public const int MaxOptions = 5;
    public const int OptionsJsonMaxLength = 2_000;

    private Question()
    {
    }

    public Guid SubjectId { get; private set; }

    public Subject? Subject { get; private set; }

    public Guid CreatedByTeacherId { get; private set; }

    public Teacher? CreatedByTeacher { get; private set; }

    public QuestionType Type { get; private set; }

    public QuestionDifficulty Difficulty { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>A JSON array of the options of a multiple choice question; <c>[]</c> for a short answer.</summary>
    public string OptionsJson { get; private set; } = "[]";

    /// <summary>The position (from 0) of the right option, or the model answer of a short answer question.</summary>
    public string AnswerKey { get; private set; } = string.Empty;

    public string Explanation { get; private set; } = string.Empty;

    public static Question Create(
        Guid subjectId, Guid teacherId, QuestionType type, QuestionDifficulty difficulty, string body, IReadOnlyList<string> options, string answerKey, string explanation)
    {
        var question = new Question { SubjectId = subjectId, CreatedByTeacherId = teacherId };
        question.Update(type, difficulty, body, options, answerKey, explanation);
        return question;
    }

    public void Update(QuestionType type, QuestionDifficulty difficulty, string body, IReadOnlyList<string> options, string answerKey, string explanation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(answerKey);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(explanation);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(body.Trim().Length, BodyMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(answerKey.Trim().Length, AnswerKeyMaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(explanation.Trim().Length, ExplanationMaxLength);

        var cleaned = options.Select(o => o.Trim()).ToList();
        if (type == QuestionType.MultipleChoice)
        {
            if (cleaned.Count is < MinOptions or > MaxOptions || cleaned.Any(o => o.Length is 0 or > OptionMaxLength)
                || cleaned.Select(o => o.ToUpperInvariant()).Distinct().Count() != cleaned.Count)
            {
                throw new ArgumentException("A multiple choice question has 2 to 5 different options.", nameof(options));
            }

            if (!int.TryParse(answerKey.Trim(), out var position) || position < 0 || position >= cleaned.Count)
            {
                throw new ArgumentException("The answer key of a multiple choice question is the position of an option.", nameof(answerKey));
            }
        }
        else if (cleaned.Count != 0)
        {
            throw new ArgumentException("A short answer question has no options.", nameof(options));
        }

        Type = type;
        Difficulty = difficulty;
        Body = body.Trim();
        OptionsJson = JsonSerializer.Serialize(cleaned);
        AnswerKey = answerKey.Trim();
        Explanation = explanation.Trim();
    }

    public IReadOnlyList<string> Options => JsonSerializer.Deserialize<List<string>>(OptionsJson) ?? [];
}
