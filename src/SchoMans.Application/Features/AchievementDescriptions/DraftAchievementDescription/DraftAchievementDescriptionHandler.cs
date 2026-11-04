using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading;
using SchoMans.Application.Features.ReportCards;

namespace SchoMans.Application.Features.AchievementDescriptions.DraftAchievementDescription;

public sealed record DraftAchievementDescriptionCommand(Guid StudentId, Guid SubjectId, Guid TermId);

/// <summary>
/// The teacher of the subject asks the AI for a draft (or a new draft) of one student's description. It replaces the text that
/// is there and waits for the teacher's review. Counts as one of the teacher's daily AI calls.
/// </summary>
public sealed class DraftAchievementDescriptionHandler(IAppDbContext db, ICurrentUser currentUser, AchievementDescriptionDrafter drafter)
{
    public async Task<Result<AchievementDescriptionDto>> Handle(DraftAchievementDescriptionCommand command, CancellationToken ct)
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

        return await drafter.DraftAsync(currentUser.UserId, command.StudentId, command.SubjectId, command.TermId, bulk: false, ct);
    }
}
