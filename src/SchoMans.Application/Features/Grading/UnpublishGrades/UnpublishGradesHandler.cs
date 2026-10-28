using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Grading.UnpublishGrades;

/// <summary>The homeroom teacher takes the grades back, so they can be corrected. Students and parents stop seeing them until they are published again.</summary>
public sealed class UnpublishGradesHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Unit>> Handle(UnpublishGradesCommand command, CancellationToken ct)
    {
        var classroom = await db.Classrooms.AsNoTracking().Where(c => c.Id == command.ClassroomId)
            .Select(c => new { c.HomeroomTeacherId }).FirstOrDefaultAsync(ct);
        if (classroom is null)
        {
            return Error.NotFound("grades.not_found", "The classroom does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != classroom.HomeroomTeacherId)
        {
            return Error.Forbidden("grades.not_homeroom", "Only the homeroom teacher unpublishes the grades of the classroom.");
        }

        var publication = await db.GradePublications.FirstOrDefaultAsync(p => p.ClassroomId == command.ClassroomId && p.TermId == command.TermId, ct);
        if (publication is null)
        {
            return Error.NotFound("grades.not_published", "The grades of this term are not published.");
        }

        db.GradePublications.Remove(publication);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
