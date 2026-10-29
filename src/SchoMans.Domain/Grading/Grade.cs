using SchoMans.Domain.Assignments;
using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Grading;

/// <summary>
/// One score of a student for a subject in a term. Scores of assignments flow in from the scored submission
/// (<see cref="SubmissionId"/> is set); the other components are entered by the teacher.
/// </summary>
public sealed class Grade : BaseEntity
{
    private Grade()
    {
    }

    public Guid StudentId { get; private set; }

    public Student? Student { get; private set; }

    public Guid SubjectId { get; private set; }

    public Subject? Subject { get; private set; }

    public Guid TermId { get; private set; }

    public Term? Term { get; private set; }

    public GradeComponent Component { get; private set; }

    public Guid? SubmissionId { get; private set; }

    public Submission? Submission { get; private set; }

    /// <summary>0 to 100.</summary>
    public decimal Score { get; private set; }

    public static Grade Create(Guid studentId, Guid subjectId, Guid termId, GradeComponent component, decimal score, Guid? submissionId = null)
    {
        var grade = new Grade
        {
            StudentId = studentId,
            SubjectId = subjectId,
            TermId = termId,
            Component = component,
            SubmissionId = submissionId,
        };
        grade.SetScore(score);
        return grade;
    }

    public void SetScore(decimal score)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "A score is between 0 and 100.");
        }

        Score = score;
    }
}
