namespace SchoMans.Domain.Assignments;

/// <summary>A classroom an assignment is given to. A plain link row: it is replaced, never edited.</summary>
public sealed class AssignmentClassroom
{
    private AssignmentClassroom()
    {
    }

    public Guid AssignmentId { get; private set; }

    public Guid ClassroomId { get; private set; }

    public static AssignmentClassroom Create(Guid assignmentId, Guid classroomId) => new() { AssignmentId = assignmentId, ClassroomId = classroomId };
}
