using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

/// <summary>Links a parent account to one of their children. One parent can have several children.</summary>
public sealed class StudentParent : BaseEntity
{
    private StudentParent()
    {
    }

    public Guid ParentUserId { get; private set; }

    public User? ParentUser { get; private set; }

    public Guid StudentId { get; private set; }

    public Student? Student { get; private set; }

    public static StudentParent Create(Guid parentUserId, Guid studentId) =>
        new() { ParentUserId = parentUserId, StudentId = studentId };
}
