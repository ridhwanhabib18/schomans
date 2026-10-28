using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Grading;

/// <summary>The homeroom teacher publishing the grades of a classroom for a term; students and parents see grades only after this exists.</summary>
public sealed class GradePublication : BaseEntity
{
    private GradePublication()
    {
    }

    public Guid ClassroomId { get; private set; }

    public Classroom? Classroom { get; private set; }

    public Guid TermId { get; private set; }

    public Term? Term { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public Guid PublishedById { get; private set; }

    public static GradePublication Create(Guid classroomId, Guid termId, Guid publishedById, DateTimeOffset now) =>
        new() { ClassroomId = classroomId, TermId = termId, PublishedById = publishedById, PublishedAt = now };
}
