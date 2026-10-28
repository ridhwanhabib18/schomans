using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Schedules.CreateSchedule;

public sealed class CreateScheduleHandler(IAppDbContext db)
{
    public async Task<Result<ScheduleDto>> Handle(CreateScheduleCommand command, CancellationToken ct)
    {
        var problem = await ScheduleRules.CheckAsync(
            db, null, command.ClassroomId, command.SubjectId, command.TeacherId, command.DayOfWeek, command.StartTime, command.EndTime, ct);
        if (problem is not null)
        {
            return problem;
        }

        var schedule = Schedule.Create(command.ClassroomId, command.SubjectId, command.TeacherId, command.DayOfWeek, command.StartTime, command.EndTime);
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return await ScheduleMapping.LoadAsync(db, schedule.Id, ct);
    }
}
