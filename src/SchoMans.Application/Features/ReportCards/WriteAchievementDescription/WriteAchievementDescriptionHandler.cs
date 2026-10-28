using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.ReportCards.WriteAchievementDescription;

/// <summary>
/// The teacher of the subject writes what a student achieved in it. The text goes on the report card, so it counts as
/// accepted as soon as the teacher saves it (AI drafts that need a review come in Phase 6). Locked once grades are published.
/// </summary>
public sealed class WriteAchievementDescriptionHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<AchievementDescriptionDto>> Handle(WriteAchievementDescriptionCommand command, CancellationToken ct)
    {
        var classroomId = await db.Students.Where(s => s.Id == command.StudentId).Select(s => (Guid?)s.ClassroomId).FirstOrDefaultAsync(ct);
        if (classroomId is null || !await db.Subjects.AnyAsync(s => s.Id == command.SubjectId, ct) || !await db.Terms.AnyAsync(t => t.Id == command.TermId, ct))
        {
            return Error.NotFound("grades.not_found", "The student, subject or term does not exist.");
        }

        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null || !await GradeBook.TeachesAsync(db, teacherId.Value, command.SubjectId, classroomId.Value, ct))
        {
            return Error.Forbidden("grades.not_your_class", "You only write for the subjects you teach in your classrooms.");
        }

        if (await GradeBook.IsPublishedAsync(db, classroomId.Value, command.TermId, ct))
        {
            return Error.Conflict("grades.published", "The grades of this term are published; unpublish them before changing a description.");
        }

        var reportCard = await db.ReportCards.Include(r => r.Descriptions).FirstOrDefaultAsync(r => r.StudentId == command.StudentId && r.TermId == command.TermId, ct);
        if (reportCard is null)
        {
            reportCard = ReportCard.Create(command.StudentId, command.TermId);
            db.ReportCards.Add(reportCard);
        }

        var description = reportCard.Descriptions.FirstOrDefault(d => d.SubjectId == command.SubjectId);
        if (description is null)
        {
            description = AchievementDescription.Create(reportCard.Id, command.SubjectId, command.Body);
            reportCard.Descriptions.Add(description);
        }
        else
        {
            description.Write(command.Body);
        }

        description.Accept();
        await db.SaveChangesAsync(ct);
        return new AchievementDescriptionDto(command.StudentId, command.SubjectId, command.TermId, description.Body);
    }
}
