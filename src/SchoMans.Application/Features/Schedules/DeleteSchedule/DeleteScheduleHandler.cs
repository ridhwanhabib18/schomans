using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Schedules.DeleteSchedule;

public sealed class DeleteScheduleHandler(IAppDbContext db)
{
    public async Task<Result<Unit>> Handle(DeleteScheduleCommand command, CancellationToken ct)
    {
        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (schedule is null)
        {
            return Error.NotFound("schedules.not_found", "The lesson does not exist.");
        }

        // Attendance history points at the lesson; removing it would leave sessions without a subject or a teacher.
        if (await db.AttendanceSessions.AnyAsync(s => s.ScheduleId == schedule.Id, ct))
        {
            return Error.Conflict("schedules.has_sessions", "Attendance was already taken for this lesson, so it cannot be deleted. Change its time instead.");
        }

        db.Schedules.Remove(schedule);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
