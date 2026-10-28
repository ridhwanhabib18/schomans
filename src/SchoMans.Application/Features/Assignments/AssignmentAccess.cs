using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Assignments;

/// <summary>
/// Who may see an assignment: the teacher who gave it, the principal, the students of its classrooms and their parents.
/// Admin staff do not work with assignments (authorization matrix).
/// </summary>
internal static class AssignmentAccess
{
    public sealed record Viewer(bool CanView, bool IsOwner, Guid? StudentId);

    /// <param name="studentId">For a parent: the child to look at. Ignored for other roles.</param>
    public static async Task<Viewer> ForAsync(IAppDbContext db, ICurrentUser user, Guid assignmentId, Guid assignmentTeacherId, Guid? studentId, CancellationToken ct)
    {
        switch (user.Role)
        {
            case UserRole.Principal:
                return new Viewer(true, false, null);
            case UserRole.Teacher:
                var isOwner = await SchoolMembership.TeacherIdAsync(db, user, ct) == assignmentTeacherId;
                return new Viewer(isOwner, isOwner, null);
            case UserRole.Student:
                var me = await SchoolMembership.StudentIdAsync(db, user, ct);
                return me is not null && await GivenToStudentAsync(db, assignmentId, me.Value, ct)
                    ? new Viewer(true, false, me)
                    : new Viewer(false, false, null);
            case UserRole.Parent:
                if (studentId is null)
                {
                    return new Viewer(false, false, null);
                }

                var isChild = await db.StudentParents.AnyAsync(p => p.ParentUserId == user.UserId && p.StudentId == studentId, ct);
                return isChild && await GivenToStudentAsync(db, assignmentId, studentId.Value, ct)
                    ? new Viewer(true, false, studentId)
                    : new Viewer(false, false, null);
            default:
                return new Viewer(false, false, null);
        }
    }

    public static Task<bool> GivenToStudentAsync(IAppDbContext db, Guid assignmentId, Guid studentId, CancellationToken ct) =>
        db.Students.AnyAsync(
            s => s.Id == studentId && db.Assignments.Any(a => a.Id == assignmentId && a.Classrooms.Any(c => c.ClassroomId == s.ClassroomId)),
            ct);
}
