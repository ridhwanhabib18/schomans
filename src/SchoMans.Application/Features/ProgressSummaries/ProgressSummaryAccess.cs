using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.ProgressSummaries;

internal static class ProgressSummaryAccess
{
    /// <summary>The summaries of a classroom are the business of its homeroom teacher alone.</summary>
    public static async Task<Error?> RequireHomeroomAsync(IAppDbContext db, ICurrentUser user, Guid classroomId, CancellationToken ct)
    {
        var homeroomTeacherId = await db.Classrooms.AsNoTracking()
            .Where(c => c.Id == classroomId)
            .Select(c => (Guid?)c.HomeroomTeacherId)
            .FirstOrDefaultAsync(ct);
        if (homeroomTeacherId is null)
        {
            return Error.NotFound("progress.classroom_not_found", "The classroom does not exist.");
        }

        var teacherId = await SchoolMembership.TeacherIdAsync(db, user, ct);
        return teacherId == homeroomTeacherId
            ? null
            : Error.Forbidden("progress.not_homeroom", "Only the homeroom teacher of the classroom works with its progress summaries.");
    }

    public static Task<Guid?> ClassroomOfStudentAsync(IAppDbContext db, Guid studentId, CancellationToken ct) =>
        db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => (Guid?)s.ClassroomId).FirstOrDefaultAsync(ct);
}
