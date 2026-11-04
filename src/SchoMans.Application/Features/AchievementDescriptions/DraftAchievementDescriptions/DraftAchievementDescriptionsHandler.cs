using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Grading;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.AchievementDescriptions.DraftAchievementDescriptions;

public sealed record DraftAchievementDescriptionsCommand(Guid ClassroomId, Guid SubjectId, Guid TermId);

/// <param name="Drafted">New drafts written.</param>
/// <param name="AlreadyHadText">Students skipped because they already have a description: a bulk run never overwrites the teacher's work.</param>
/// <param name="NoScores">Students skipped because there are no scores to write from.</param>
/// <param name="Failed">Students whose draft the AI could not give (an unusable answer); try again for them.</param>
/// <param name="StoppedBy">The error code when the run stopped early (the daily limit or the month's budget), otherwise null.</param>
public sealed record DraftedDescriptionsDto(int Drafted, int AlreadyHadText, int NoScores, int Failed, string? StoppedBy);

/// <summary>
/// Drafts the descriptions of a whole classroom for one subject and term. The first AI call counts as one of the teacher's daily
/// calls, the rest of the run does not (they still count toward the month's budget). Students who already have a description are left alone.
/// </summary>
public sealed class DraftAchievementDescriptionsHandler(IAppDbContext db, ICurrentUser currentUser, AchievementDescriptionDrafter drafter)
{
    public async Task<Result<DraftedDescriptionsDto>> Handle(DraftAchievementDescriptionsCommand command, CancellationToken ct)
    {
        if (!await db.Classrooms.AnyAsync(c => c.Id == command.ClassroomId, ct)
            || !await db.Subjects.AnyAsync(s => s.Id == command.SubjectId, ct)
            || !await db.Terms.AnyAsync(t => t.Id == command.TermId, ct))
        {
            return Error.NotFound("grades.not_found", "The classroom, subject or term does not exist.");
        }

        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null || !await GradeBook.TeachesAsync(db, teacherId.Value, command.SubjectId, command.ClassroomId, ct))
        {
            return Error.Forbidden("grades.not_your_class", "You only write for the subjects you teach in your classrooms.");
        }

        if (await GradeBook.IsPublishedAsync(db, command.ClassroomId, command.TermId, ct))
        {
            return Error.Conflict("grades.published", "The grades of this term are published; unpublish them before changing a description.");
        }

        var studentIds = await db.Students.AsNoTracking()
            .Where(s => s.ClassroomId == command.ClassroomId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.User!.FullName)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var haveText = (await db.ReportCards.AsNoTracking()
                .Where(r => r.TermId == command.TermId && studentIds.Contains(r.StudentId) && r.Descriptions.Any(d => d.SubjectId == command.SubjectId))
                .Select(r => r.StudentId)
                .ToListAsync(ct))
            .ToHashSet();

        int drafted = 0, alreadyHadText = 0, noScores = 0, failed = 0;
        string? stoppedBy = null;
        var firstCall = true;
        foreach (var studentId in studentIds)
        {
            if (haveText.Contains(studentId))
            {
                alreadyHadText++;
                continue;
            }

            var result = await drafter.DraftAsync(currentUser.UserId, studentId, command.SubjectId, command.TermId, bulk: !firstCall, ct);
            if (result.IsSuccess)
            {
                drafted++;
                firstCall = false;
            }
            else if (result.Error.Code == AchievementDescriptionDrafter.NoDataCode)
            {
                noScores++;
            }
            else if (result.Error.Code is "ai.budget_exhausted" or "ai.daily_limit")
            {
                stoppedBy = result.Error.Code;
                break;
            }
            else
            {
                failed++;
                firstCall = false;
            }
        }

        return new DraftedDescriptionsDto(drafted, alreadyHadText, noScores, failed, stoppedBy);
    }
}
