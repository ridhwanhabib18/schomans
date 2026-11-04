using SchoMans.Domain.Assignments;
using SchoMans.Domain.Grading;

namespace SchoMans.Domain.Tests.Assignments;

public class QuizTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Teacher = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();
    private static readonly Guid Term = Guid.NewGuid();

    private static Assignment Quiz() => Assignment.CreateQuiz(Teacher, Subject, Term, "Cells", "Chapter 3", Now.AddDays(3), [Guid.NewGuid()]);

    [Fact]
    public void A_quiz_is_an_assignment_of_type_quiz_that_counts_in_the_quiz_component()
    {
        var quiz = Quiz();

        Assert.Equal((AssignmentType.Quiz, GradeComponent.Quiz, true), (quiz.Type, quiz.Component, quiz.IsQuiz));
    }

    [Fact]
    public void An_ordinary_assignment_is_not_a_quiz()
    {
        var work = Assignment.Create(Teacher, Subject, Term, "Essay", "", Now.AddDays(3), [Guid.NewGuid()]);

        Assert.Equal((AssignmentType.Regular, GradeComponent.Assignment, false), (work.Type, work.Component, work.IsQuiz));
    }

    [Fact]
    public void A_question_of_a_quiz_has_a_place_and_points_within_the_limits()
    {
        var question = AssignmentQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), 2, 5);

        Assert.Equal((2, 5), (question.Order, question.Points));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssignmentQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssignmentQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssignmentQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), 1, AssignmentQuestion.MaxPoints + 1));
    }

    [Fact]
    public void An_attempt_at_a_quiz_keeps_the_answers_and_is_not_late_or_scored()
    {
        var attempt = Submission.CreateQuiz(Guid.NewGuid(), Guid.NewGuid(), """{"a":"1"}""", Now);

        Assert.True(attempt.IsQuiz);
        Assert.Equal((Now, false, false, null), (attempt.SubmittedAt, attempt.IsLate, attempt.IsGraded, attempt.Score));
        Assert.Equal("""{"a":"1"}""", attempt.AnswersJson);
    }

    [Fact]
    public void An_attempt_cannot_be_replaced_by_handing_in_again()
    {
        var quiz = Quiz();
        var attempt = Submission.CreateQuiz(quiz.Id, Guid.NewGuid(), """{"a":"1"}""", Now);

        Assert.False(attempt.Replace(quiz, null, null, "a new answer", Now.AddHours(1)));
        Assert.Equal("""{"a":"1"}""", attempt.AnswersJson);
        Assert.Null(attempt.TextBody);
    }

    [Fact]
    public void A_quiz_is_completed_with_a_score_by_the_teacher_or_by_nobody()
    {
        var byTeacher = Submission.CreateQuiz(Guid.NewGuid(), Guid.NewGuid(), "{}", Now);
        var teacherUser = Guid.NewGuid();
        byTeacher.CompleteQuiz(75.5m, teacherUser, Now.AddDays(1));
        var automatic = Submission.CreateQuiz(Guid.NewGuid(), Guid.NewGuid(), "{}", Now);
        automatic.CompleteQuiz(100, null, Now);

        Assert.Equal((75.5m, true, teacherUser), (byTeacher.Score, byTeacher.IsGraded, byTeacher.GradedById));
        Assert.Equal((100m, true, null), (automatic.Score, automatic.IsGraded, automatic.GradedById));
    }

    [Fact]
    public void A_score_is_between_0_and_100_and_only_a_quiz_is_completed_this_way()
    {
        var attempt = Submission.CreateQuiz(Guid.NewGuid(), Guid.NewGuid(), "{}", Now);
        var work = Submission.Create(Quiz(), Guid.NewGuid(), null, null, "text", Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => attempt.CompleteQuiz(100.01m, null, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => attempt.CompleteQuiz(-1, null, Now));
        Assert.Throws<InvalidOperationException>(() => work.CompleteQuiz(50, null, Now));
        Assert.Throws<InvalidOperationException>(() => work.SetShortPoints("{}"));
    }

    [Fact]
    public void The_points_of_the_short_answers_are_kept_on_the_attempt()
    {
        var attempt = Submission.CreateQuiz(Guid.NewGuid(), Guid.NewGuid(), "{}", Now);

        attempt.SetShortPoints("""{"q":1.5}""");

        Assert.Equal("""{"q":1.5}""", attempt.ShortPointsJson);
        Assert.Throws<ArgumentException>(() => attempt.SetShortPoints(" "));
    }

    [Fact]
    public void An_attempt_needs_answers_within_the_limit()
    {
        Assert.Throws<ArgumentException>(() => Submission.CreateQuiz(Guid.NewGuid(), Guid.NewGuid(), " ", Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => Submission.CreateQuiz(Guid.NewGuid(), Guid.NewGuid(), new string('x', Submission.AnswersJsonMaxLength + 1), Now));
    }
}
