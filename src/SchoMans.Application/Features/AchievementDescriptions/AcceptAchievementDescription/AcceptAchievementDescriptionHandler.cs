using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading;
using SchoMans.Application.Features.ReportCards;

namespace SchoMans.Application.Features.AchievementDescriptions.AcceptAchievementDescription;

public sealed record AcceptAchievementDescriptionCommand(Guid StudentId, Guid SubjectId, Guid TermId);

/// <summary>The teacher of the subject reviews a draft and accepts it as it is; only then does it go on the report card.</summary>
public sealed class AcceptAchievementDescriptionHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<AchievementDescriptionDto>> Handle(AcceptAchievementDescriptionCommand command, CancellationToken ct)
    {
        var classroomId = await db.Students.Where(s => s.Id == command.StudentId).Select(s => (Guid?)s.ClassroomId).FirstOrDefaultAsync(ct);
        if (classroomId is null)
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

        var description = await db.ReportCards
            .Where(r => r.StudentId == command.StudentId && r.TermId == command.TermId)
            .SelectMany(r => r.Descriptions.Where(d => d.SubjectId == command.SubjectId))
            .FirstOrDefaultAsync(ct);
        if (description is null)
        {
            return Error.NotFound("achievement.not_found", "There is no description to accept yet.");
        }

        description.Accept();
        await db.SaveChangesAsync(ct);
        return new AchievementDescriptionDto(command.StudentId, command.SubjectId, command.TermId, description.Body, description.Status, description.IsAiDrafted);
    }
}
