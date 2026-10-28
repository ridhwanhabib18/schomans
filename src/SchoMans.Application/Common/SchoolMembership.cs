using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Common;

/// <summary>
/// Who the signed-in user is inside the school: their teacher record, their student record, the children of a parent,
/// and which classrooms a teacher works in. These answers drive the ownership checks of the second authorization layer.
/// </summary>
internal static class SchoolMembership
{
    public static Task<Guid?> TeacherIdAsync(IAppDbContext db, ICurrentUser user, CancellationToken ct) =>
        db.Teachers.AsNoTracking().Where(t => t.UserId == user.UserId).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);

    public static Task<Guid?> StudentIdAsync(IAppDbContext db, ICurrentUser user, CancellationToken ct) =>
        db.Students.AsNoTracking().Where(s => s.UserId == user.UserId).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);

    public static Task<List<Guid>> ChildIdsAsync(IAppDbContext db, ICurrentUser user, CancellationToken ct) =>
        db.StudentParents.AsNoTracking().Where(p => p.ParentUserId == user.UserId).Select(p => p.StudentId).ToListAsync(ct);

    /// <summary>The classrooms a teacher works in: the ones they are homeroom teacher of and the ones they teach in the timetable.</summary>
    public static IQueryable<Guid> TeacherClassroomIds(IAppDbContext db, Guid teacherId) =>
        db.Classrooms.Where(c => c.HomeroomTeacherId == teacherId).Select(c => c.Id)
            .Union(db.Schedules.Where(s => s.TeacherId == teacherId).Select(s => s.ClassroomId));

    /// <summary>
    /// True when the signed-in user may look at this student's own records (attendance, assignments): the student, a
    /// parent of the student, admin staff and the principal, and a teacher who works in the student's classroom.
    /// </summary>
    public static async Task<bool> CanSeeStudentAsync(IAppDbContext db, ICurrentUser user, Guid studentId, CancellationToken ct)
    {
        switch (user.Role)
        {
            case UserRole.Admin or UserRole.Principal:
                return true;
            case UserRole.Student:
                return await StudentIdAsync(db, user, ct) == studentId;
            case UserRole.Parent:
                return await db.StudentParents.AnyAsync(p => p.ParentUserId == user.UserId && p.StudentId == studentId, ct);
            case UserRole.Teacher:
                var teacherId = await TeacherIdAsync(db, user, ct);
                if (teacherId is null)
                {
                    return false;
                }

                var classroomId = await db.Students.Where(s => s.Id == studentId).Select(s => (Guid?)s.ClassroomId).FirstOrDefaultAsync(ct);
                return classroomId is not null && await TeacherClassroomIds(db, teacherId.Value).ContainsAsync(classroomId.Value, ct);
            default:
                return false;
        }
    }
}
