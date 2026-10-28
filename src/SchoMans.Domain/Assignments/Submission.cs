using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Assignments;

/// <summary>
/// A student's work for an assignment: a file (PDF or image) or a text, or both. One per student and assignment; the
/// student can replace it until the teacher has scored it. Handing it in after the due date marks it late.
/// </summary>
public sealed class Submission : BaseEntity
{
    public const int TextMaxLength = 20_000;
    public const int FeedbackMaxLength = 2_000;

    private Submission()
    {
    }

    public Guid AssignmentId { get; private set; }

    public Assignment? Assignment { get; private set; }

    public Guid StudentId { get; private set; }

    public Student? Student { get; private set; }

    public string? FileKey { get; private set; }

    public string? FileName { get; private set; }

    public string? TextBody { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public bool IsLate { get; private set; }

    /// <summary>0 to 100; set by the teacher. It flows into the subject grade in Phase 4.</summary>
    public decimal? Score { get; private set; }

    public string? Feedback { get; private set; }

    public DateTimeOffset? GradedAt { get; private set; }

    public Guid? GradedById { get; private set; }

    public bool IsGraded => GradedAt is not null;

    public static Submission Create(Assignment assignment, Guid studentId, string? fileKey, string? fileName, string? textBody, DateTimeOffset now)
    {
        var submission = new Submission { AssignmentId = assignment.Id, StudentId = studentId };
        submission.Replace(assignment, fileKey, fileName, textBody, now);
        return submission;
    }

    /// <summary>Hands in new work instead of the old. Returns false once the work is scored: then it is final.</summary>
    public bool Replace(Assignment assignment, string? fileKey, string? fileName, string? textBody, DateTimeOffset now)
    {
        if (IsGraded)
        {
            return false;
        }

        var text = string.IsNullOrWhiteSpace(textBody) ? null : textBody.Trim();
        if (fileKey is null && text is null)
        {
            throw new ArgumentException("A submission has a file or a text.");
        }

        FileKey = fileKey;
        FileName = fileKey is null ? null : fileName?.Trim();
        TextBody = text;
        SubmittedAt = now;
        IsLate = assignment.IsLateAt(now);
        return true;
    }

    /// <summary>The teacher scores the work; a score can be corrected later.</summary>
    public void Grade(decimal score, string? feedback, Guid gradedById, DateTimeOffset now)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "A score is between 0 and 100.");
        }

        Score = score;
        Feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        GradedAt = now;
        GradedById = gradedById;
    }
}
