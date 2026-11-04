using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

/// <summary>
/// A short note a teacher keeps about a student: the homeroom teacher and the teachers of the student's classroom write
/// them. The AI drafts of Phase 6 (progress summaries, achievement descriptions) read them; parents never see a note
/// itself, only what the teacher approves in a summary.
/// </summary>
public sealed class TeacherNote : BaseEntity
{
    public const int BodyMaxLength = 500;

    private TeacherNote()
    {
    }

    public Guid StudentId { get; private set; }

    public Student? Student { get; private set; }

    /// <summary>The user (a teacher) who wrote the note.</summary>
    public Guid AuthorId { get; private set; }

    public User? Author { get; private set; }

    /// <summary>The school day the note is about; the month of this date decides which summary uses it.</summary>
    public DateOnly Date { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public static TeacherNote Create(Guid studentId, Guid authorId, DateOnly date, string body)
    {
        var note = new TeacherNote { StudentId = studentId, AuthorId = authorId, Date = date };
        note.Edit(body);
        return note;
    }

    public void Edit(string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (body.Trim().Length > BodyMaxLength)
        {
            throw new ArgumentException($"A note has at most {BodyMaxLength} characters.", nameof(body));
        }

        Body = body.Trim();
    }
}
