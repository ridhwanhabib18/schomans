using System.Text.Json;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Quizzes;

public sealed record QuizQuestionInput(Guid QuestionId, int Points = 1);

/// <param name="Questions">Questions of the bank of the subject, in the order they are asked; each worth its points (1 unless changed).</param>
public sealed record CreateQuizCommand(string Title, string? Description, DateTimeOffset DueAt, Guid SubjectId, List<Guid> ClassroomIds, List<QuizQuestionInput> Questions);

/// <param name="Answer">For multiple choice the position (from 0) of the chosen option; for short answer the text; null when left empty.</param>
public sealed record QuizAnswerInput(Guid QuestionId, string? Answer);

public sealed record SubmitQuizCommand(Guid AssignmentId, List<QuizAnswerInput> Answers);

public sealed record ShortPointsInput(Guid QuestionId, decimal Points);

/// <param name="Points">Points for short answers, 0 up to what the question is worth. A quiz is scored once every short answer has points.</param>
public sealed record GradeQuizCommand(Guid AssignmentId, Guid StudentId, List<ShortPointsInput> Points);

/// <param name="StudentId">For the teacher of the quiz and the principal: whose attempt to open. A parent names their child.</param>
public sealed record GetQuizQuery(Guid AssignmentId, Guid? StudentId = null);

/// <summary>
/// One question of a quiz as the viewer may see it. The answer key and the explanation are null while the quiz is open for a student:
/// they are shown to a student, and to a parent, only after the due date; the teacher always sees them.
/// </summary>
public sealed record QuizQuestionDto(
    Guid QuestionId,
    int Order,
    QuestionType Type,
    QuestionDifficulty Difficulty,
    string Body,
    List<string> Options,
    int Points,
    string? AnswerKey,
    string? Explanation,
    string? MyAnswer,
    decimal? PointsAwarded);

public enum QuizAttemptStatus
{
    /// <summary>Handed in. A student sees nothing more of it until the due date.</summary>
    Submitted,
    WaitingForTeacher,
    Scored,
}

/// <param name="ShortAnswersToGrade">How many short answers still wait for points; 0 when the quiz is scored.</param>
public sealed record QuizAttemptDto(Guid SubmissionId, Guid StudentId, string StudentName, DateTimeOffset SubmittedAt, QuizAttemptStatus Status, decimal? Score, int ShortAnswersToGrade);

/// <param name="IsOpen">Before the due date; a quiz is taken once and can no longer be taken after it.</param>
/// <param name="ResultsVisible">The due date has passed: a student sees the key, the explanations, the points and the score.</param>
public sealed record QuizDto(
    Guid AssignmentId,
    string Title,
    string Description,
    DateTimeOffset DueAt,
    bool IsOpen,
    bool ResultsVisible,
    bool CanEdit,
    int TotalPoints,
    List<QuizQuestionDto> Questions,
    QuizAttemptDto? Attempt);

/// <summary>The scoring of a quiz: multiple choice by the answer key, short answers by the points the teacher gives.</summary>
internal static class QuizScoring
{
    public sealed record Row(Guid QuestionId, int Order, QuestionType Type, QuestionDifficulty Difficulty, string Body, List<string> Options, string AnswerKey, string Explanation, int Points);

    public static Dictionary<Guid, string?> ReadAnswers(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<Dictionary<Guid, string?>>(json) ?? [];

    public static Dictionary<Guid, decimal> ReadShortPoints(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(json) ?? [];

    public static bool IsRight(Row q, IReadOnlyDictionary<Guid, string?> answers) =>
        q.Type == QuestionType.MultipleChoice && answers.TryGetValue(q.QuestionId, out var answer) && answer is not null && answer.Trim() == q.AnswerKey.Trim();

    /// <summary>The points of one question; null for a short answer that has not been given points yet.</summary>
    public static decimal? PointsOf(Row q, IReadOnlyDictionary<Guid, string?> answers, IReadOnlyDictionary<Guid, decimal> shortPoints) =>
        q.Type == QuestionType.MultipleChoice
            ? (IsRight(q, answers) ? q.Points : 0)
            : shortPoints.TryGetValue(q.QuestionId, out var points) ? points : null;

    public static int ShortAnswersToGrade(IEnumerable<Row> rows, IReadOnlyDictionary<Guid, decimal> shortPoints) =>
        rows.Count(q => q.Type == QuestionType.ShortAnswer && !shortPoints.ContainsKey(q.QuestionId));

    /// <summary>0 to 100 with two decimals: the points earned over the points of the quiz.</summary>
    public static decimal Score(IReadOnlyList<Row> rows, IReadOnlyDictionary<Guid, string?> answers, IReadOnlyDictionary<Guid, decimal> shortPoints)
    {
        var total = rows.Sum(q => q.Points);
        var earned = rows.Sum(q => PointsOf(q, answers, shortPoints) ?? 0);
        return total == 0 ? 0 : decimal.Round(earned * 100m / total, 2, MidpointRounding.AwayFromZero);
    }
}
