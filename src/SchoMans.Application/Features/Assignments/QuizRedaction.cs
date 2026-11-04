namespace SchoMans.Application.Features.Assignments;

/// <summary>A student, and a parent, see nothing of the result of a quiz before its due date: not the score, not the feedback.</summary>
internal static class QuizRedaction
{
    public static SubmissionDto Hide(SubmissionDto submission) => submission with { Score = null, Feedback = null, GradedAt = null };
}
