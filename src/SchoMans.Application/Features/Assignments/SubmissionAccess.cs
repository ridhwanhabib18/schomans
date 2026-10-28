using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Assignments;

/// <summary>Who may open a piece of handed-in work: the student, their parents, the teacher of the assignment, the principal.</summary>
internal static class SubmissionAccess
{
    public static async Task<bool> CanViewAsync(IAppDbContext db, ICurrentUser user, Guid studentId, Guid assignmentTeacherId, CancellationToken ct) =>
        user.Role switch
        {
            UserRole.Principal => true,
            UserRole.Teacher => await SchoolMembership.TeacherIdAsync(db, user, ct) == assignmentTeacherId,
            UserRole.Student => await SchoolMembership.StudentIdAsync(db, user, ct) == studentId,
            UserRole.Parent => await db.StudentParents.AnyAsync(p => p.ParentUserId == user.UserId && p.StudentId == studentId, ct),
            _ => false,
        };
}
