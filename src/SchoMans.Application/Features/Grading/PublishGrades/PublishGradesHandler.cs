using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.Grading.PublishGrades;

/// <summary>The homeroom teacher publishes the grades of the classroom for a term. From then on students and parents see them and they are locked.</summary>
public sealed class PublishGradesHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<GradePublicationDto>> Handle(PublishGradesCommand command, CancellationToken ct)
    {
        var classroom = await db.Classrooms.AsNoTracking().Where(c => c.Id == command.ClassroomId)
            .Select(c => new { c.HomeroomTeacherId }).FirstOrDefaultAsync(ct);
        if (classroom is null || !await db.Terms.AnyAsync(t => t.Id == command.TermId, ct))
        {
            return Error.NotFound("grades.not_found", "The classroom or term does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != classroom.HomeroomTeacherId)
        {
            return Error.Forbidden("grades.not_homeroom", "Only the homeroom teacher publishes the grades of the classroom.");
        }

        if (await GradeBook.IsPublishedAsync(db, command.ClassroomId, command.TermId, ct))
        {
            return Error.Conflict("grades.already_published", "The grades of this term are already published.");
        }

        var publication = GradePublication.Create(command.ClassroomId, command.TermId, currentUser.UserId, clock.UtcNow);
        db.GradePublications.Add(publication);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("grades.already_published", "The grades of this term are already published.");
        }

        return new GradePublicationDto(publication.ClassroomId, publication.TermId, publication.PublishedAt);
    }
}
