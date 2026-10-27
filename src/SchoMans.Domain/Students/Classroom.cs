using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

public sealed class Classroom : BaseEntity
{
    private Classroom()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public Guid AcademicYearId { get; private set; }

    public AcademicYear? AcademicYear { get; private set; }

    public Guid HomeroomTeacherId { get; private set; }

    public Teacher? HomeroomTeacher { get; private set; }

    public static Classroom Create(string name, Guid academicYearId, Guid homeroomTeacherId)
    {
        var classroom = new Classroom { AcademicYearId = academicYearId };
        classroom.Update(name, homeroomTeacherId);
        return classroom;
    }

    public void Update(string name, Guid homeroomTeacherId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        HomeroomTeacherId = homeroomTeacherId;
    }
}
