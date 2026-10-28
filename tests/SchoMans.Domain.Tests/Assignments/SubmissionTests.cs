using SchoMans.Domain.Assignments;

namespace SchoMans.Domain.Tests.Assignments;

public class SubmissionTests
{
    private static readonly DateTimeOffset Due = new(2026, 11, 2, 16, 59, 0, TimeSpan.Zero);
    private static readonly Guid Student = Guid.NewGuid();
    private static readonly Guid Teacher = Guid.NewGuid();

    private static readonly Assignment Assignment =
        Assignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Fractions", "", Due, [Guid.NewGuid()]);

    [Fact]
    public void Work_handed_in_before_the_due_date_is_on_time()
    {
        var submission = Submission.Create(Assignment, Student, "key", " answer.pdf ", null, Due.AddHours(-1));

        Assert.False(submission.IsLate);
        Assert.Equal("answer.pdf", submission.FileName);
        Assert.Null(submission.TextBody);
        Assert.False(submission.IsGraded);
    }

    [Fact]
    public void Work_handed_in_after_the_due_date_is_late()
    {
        Assert.True(Submission.Create(Assignment, Student, null, null, "My answer", Due.AddMinutes(1)).IsLate);
    }

    [Fact]
    public void Work_needs_a_file_or_a_text()
    {
        Assert.Throws<ArgumentException>(() => Submission.Create(Assignment, Student, null, null, "   ", Due));
    }

    [Fact]
    public void Handing_in_again_replaces_the_work_and_its_lateness()
    {
        var submission = Submission.Create(Assignment, Student, "key1", "a.pdf", null, Due.AddHours(-1));

        Assert.True(submission.Replace(Assignment, null, "ignored.pdf", "  Typed answer  ", Due.AddHours(1)));

        Assert.Null(submission.FileKey);
        Assert.Null(submission.FileName);
        Assert.Equal("Typed answer", submission.TextBody);
        Assert.True(submission.IsLate);
        Assert.Equal(Due.AddHours(1), submission.SubmittedAt);
    }

    [Fact]
    public void Scored_work_is_final()
    {
        var submission = Submission.Create(Assignment, Student, "key", "a.pdf", null, Due.AddHours(-1));
        submission.Grade(80, " Good ", Teacher, Due.AddDays(1));

        Assert.False(submission.Replace(Assignment, "key2", "b.pdf", null, Due.AddDays(2)));

        Assert.Equal("key", submission.FileKey);
        Assert.Equal(80, submission.Score);
        Assert.Equal("Good", submission.Feedback);
        Assert.True(submission.IsGraded);
        Assert.Equal(Teacher, submission.GradedById);
    }

    [Fact]
    public void A_score_can_be_corrected_and_stays_between_0_and_100()
    {
        var submission = Submission.Create(Assignment, Student, "key", "a.pdf", null, Due);
        submission.Grade(70, null, Teacher, Due.AddDays(1));

        submission.Grade(100, "", Teacher, Due.AddDays(2));

        Assert.Equal(100, submission.Score);
        Assert.Null(submission.Feedback);
        Assert.Throws<ArgumentOutOfRangeException>(() => submission.Grade(101, null, Teacher, Due));
        Assert.Throws<ArgumentOutOfRangeException>(() => submission.Grade(-1, null, Teacher, Due));
    }
}
