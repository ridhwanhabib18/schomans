using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.RegenerateProgressSummary;

/// <summary>The homeroom teacher asks for a (new) draft. It counts toward the teacher's daily AI calls, unlike the monthly job.</summary>
public sealed class RegenerateProgressSummaryHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock, ProgressSummaryDrafter drafter)
{
    public async Task<Result<ProgressSummaryDto>> Handle(RegenerateProgressSummaryCommand command, CancellationToken ct)
    {
        var classroomId = await ProgressSummaryAccess.ClassroomOfStudentAsync(db, command.StudentId, ct);
        if (classroomId is null)
        {
            return Error.NotFound("progress.student_not_found", "The student does not exist.");
        }

        if (await ProgressSummaryAccess.RequireHomeroomAsync(db, currentUser, classroomId.Value, ct) is { } denied)
        {
            return denied;
        }

        if (string.CompareOrdinal(command.Month, ProgressSummary.MonthKey(clock.Today)) > 0)
        {
            return Error.Validation("progress.future_month", "A summary cannot be written for a month that has not started.");
        }

        var result = await drafter.DraftAsync(currentUser.UserId, command.StudentId, command.Month, ct);
        if (!result.IsSuccess)
        {
            return result.Error;
        }

        var name = await db.Students.AsNoTracking().Where(s => s.Id == command.StudentId).Select(s => s.User!.FullName).FirstAsync(ct);
        return ProgressSummaryMapping.ToDto(result.Value, name);
    }
}
