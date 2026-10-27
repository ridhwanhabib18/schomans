using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

public sealed class Student : BaseEntity
{
    private Student()
    {
    }

    public Guid UserId { get; private set; }

    public User? User { get; private set; }

    public string Nisn { get; private set; } = string.Empty;

    public Guid ClassroomId { get; private set; }

    public Classroom? Classroom { get; private set; }

    public DateOnly DateOfBirth { get; private set; }

    public StudentStatus Status { get; private set; } = StudentStatus.Active;

    public static Student Create(Guid userId, string nisn, Guid classroomId, DateOnly dateOfBirth)
    {
        var student = new Student { UserId = userId, ClassroomId = classroomId };
        student.UpdateDetails(nisn, dateOfBirth);
        return student;
    }

    public void UpdateDetails(string nisn, DateOnly dateOfBirth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nisn);

        Nisn = nisn.Trim();
        DateOfBirth = dateOfBirth;
    }

    /// <summary>Moves an active student to another classroom. A graduated student cannot be moved.</summary>
    public bool MoveTo(Guid classroomId)
    {
        if (Status != StudentStatus.Active)
        {
            return false;
        }

        ClassroomId = classroomId;
        return true;
    }

    /// <summary>Marks the student as graduated; the last classroom stays on the record.</summary>
    public bool Graduate()
    {
        if (Status == StudentStatus.Graduated)
        {
            return false;
        }

        Status = StudentStatus.Graduated;
        return true;
    }
}
