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

    public static Student Create(Guid userId, string nisn, Guid classroomId, DateOnly dateOfBirth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nisn);

        return new Student
        {
            UserId = userId,
            Nisn = nisn.Trim(),
            ClassroomId = classroomId,
            DateOfBirth = dateOfBirth,
        };
    }
}
