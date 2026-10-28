using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Schedules.UpdateSchedule;

public sealed class UpdateScheduleHandler(IAppDbContext db)
{
    public async Task<Result<ScheduleDto>> Handle(UpdateScheduleCommand command, CancellationToken ct)
    {
        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (schedule is null)
        {
            return Error.NotFound("schedules.not_found", "The lesson does not exist.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        var problem = await ScheduleRules.CheckAsync(
            db, schedule.Id, schedule.ClassroomId, command.SubjectId, command.TeacherId, command.DayOfWeek, command.StartTime, command.EndTime, ct);
        if (problem is not null)
        {
            return problem;
        }

        schedule.Update(command.SubjectId, command.TeacherId, command.DayOfWeek, command.StartTime, command.EndTime);
        db.SetExpectedRowVersion(schedule, rowVersion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommonErrors.ConcurrencyConflict;
        }

        return await ScheduleMapping.LoadAsync(db, schedule.Id, ct);
    }
}
