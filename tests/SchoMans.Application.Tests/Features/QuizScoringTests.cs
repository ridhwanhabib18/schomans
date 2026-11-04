using SchoMans.Application.Features.Quizzes;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Tests.Features;

/// <summary>How a quiz is scored: multiple choice by the answer key, short answers by the points of the teacher, as a score from 0 to 100.</summary>
public class QuizScoringTests
{
    private static readonly Guid Mc1 = Guid.NewGuid();
    private static readonly Guid Mc2 = Guid.NewGuid();
    private static readonly Guid Short = Guid.NewGuid();

    private static QuizScoring.Row Choice(Guid id, int order, string key, int points = 1) =>
        new(id, order, QuestionType.MultipleChoice, QuestionDifficulty.Easy, "Q", ["a", "b", "c"], key, "", points);

    private static QuizScoring.Row ShortRow(int points = 2) =>
        new(Short, 3, QuestionType.ShortAnswer, QuestionDifficulty.Easy, "Q", [], "model", "", points);

    private static readonly IReadOnlyDictionary<Guid, decimal> NoPoints = new Dictionary<Guid, decimal>();

    [Fact]
    public void A_multiple_choice_answer_is_right_when_it_is_the_key()
    {
        var q = Choice(Mc1, 1, "1");

        Assert.True(QuizScoring.IsRight(q, new Dictionary<Guid, string?> { [Mc1] = "1" }));
        Assert.True(QuizScoring.IsRight(q, new Dictionary<Guid, string?> { [Mc1] = " 1 " }));
        Assert.False(QuizScoring.IsRight(q, new Dictionary<Guid, string?> { [Mc1] = "2" }));
        Assert.False(QuizScoring.IsRight(q, new Dictionary<Guid, string?> { [Mc1] = null }));
        Assert.False(QuizScoring.IsRight(q, new Dictionary<Guid, string?>()));
    }

    [Fact]
    public void A_short_answer_is_never_right_by_itself()
    {
        Assert.False(QuizScoring.IsRight(ShortRow(), new Dictionary<Guid, string?> { [Short] = "model" }));
    }

    [Fact]
    public void A_right_answer_earns_the_points_of_its_question_and_a_wrong_or_empty_one_nothing()
    {
        var rows = new[] { Choice(Mc1, 1, "0", 3), Choice(Mc2, 2, "2", 4) };
        var answers = new Dictionary<Guid, string?> { [Mc1] = "0", [Mc2] = null };

        Assert.Equal(3m, QuizScoring.PointsOf(rows[0], answers, NoPoints));
        Assert.Equal(0m, QuizScoring.PointsOf(rows[1], answers, NoPoints));
    }

    [Fact]
    public void A_short_answer_has_no_points_until_the_teacher_gives_them()
    {
        var q = ShortRow();

        Assert.Null(QuizScoring.PointsOf(q, new Dictionary<Guid, string?>(), NoPoints));
        Assert.Equal(1.5m, QuizScoring.PointsOf(q, new Dictionary<Guid, string?>(), new Dictionary<Guid, decimal> { [Short] = 1.5m }));
        Assert.Equal(0m, QuizScoring.PointsOf(q, new Dictionary<Guid, string?>(), new Dictionary<Guid, decimal> { [Short] = 0 }));
    }

    [Fact]
    public void Short_answers_without_points_are_counted()
    {
        var rows = new[] { Choice(Mc1, 1, "0"), ShortRow() };

        Assert.Equal(1, QuizScoring.ShortAnswersToGrade(rows, NoPoints));
        Assert.Equal(0, QuizScoring.ShortAnswersToGrade(rows, new Dictionary<Guid, decimal> { [Short] = 0 }));
        Assert.Equal(0, QuizScoring.ShortAnswersToGrade([Choice(Mc1, 1, "0")], NoPoints));
    }

    [Fact]
    public void The_score_is_the_points_earned_over_the_points_of_the_quiz_times_100()
    {
        var rows = new[] { Choice(Mc1, 1, "0"), Choice(Mc2, 2, "2"), ShortRow(2) };
        var answers = new Dictionary<Guid, string?> { [Mc1] = "0", [Mc2] = "1" }; // one right, one wrong

        Assert.Equal(25m, QuizScoring.Score(rows, answers, NoPoints)); // 1 of 4 while the short answer has no points
        Assert.Equal(75m, QuizScoring.Score(rows, answers, new Dictionary<Guid, decimal> { [Short] = 2 }));
        Assert.Equal(50m, QuizScoring.Score(rows, answers, new Dictionary<Guid, decimal> { [Short] = 1 }));
    }

    [Fact]
    public void The_score_is_rounded_to_two_decimals_and_a_perfect_quiz_is_100()
    {
        var rows = new[] { Choice(Mc1, 1, "0"), Choice(Mc2, 2, "0"), Choice(Guid.NewGuid(), 3, "0") };
        var first = rows[0].QuestionId;

        Assert.Equal(33.33m, QuizScoring.Score(rows, new Dictionary<Guid, string?> { [first] = "0" }, NoPoints));
        Assert.Equal(100m, QuizScoring.Score(rows, rows.ToDictionary(r => r.QuestionId, _ => (string?)"0"), NoPoints));
        Assert.Equal(0m, QuizScoring.Score([], new Dictionary<Guid, string?>(), NoPoints));
    }

    [Fact]
    public void Answers_and_points_round_trip_through_json_and_empty_json_is_nothing()
    {
        Assert.Empty(QuizScoring.ReadAnswers(null));
        Assert.Empty(QuizScoring.ReadShortPoints(""));
        Assert.Equal(1.5m, QuizScoring.ReadShortPoints($$"""{"{{Short}}": 1.5}""")[Short]);
        Assert.Null(QuizScoring.ReadAnswers($$"""{"{{Mc1}}": null}""")[Mc1]);
    }
}
