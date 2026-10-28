using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Schedules.ListSchedules;

/// <summary>Staff read the whole timetable (a teacher needs to see when classrooms are free); only admin staff change it.</summary>
public sealed class ListSchedulesHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<ScheduleDto>>> Handle(ListSchedulesQuery query, CancellationToken ct)
    {
        if (currentUser.Role is not (UserRole.Admin or UserRole.Principal or UserRole.Teacher))
        {
            return Error.Forbidden("schedules.forbidden", "You are not allowed to see the timetable.");
        }

        var schedules = db.Schedules.AsNoTracking();
        if (query.ClassroomId is { } classroomId)
        {
            schedules = schedules.Where(s => s.ClassroomId == classroomId);
        }

        if (query.TeacherId is { } teacherId)
        {
            schedules = schedules.Where(s => s.TeacherId == teacherId);
        }

        if (query.DayOfWeek is { } day)
        {
            schedules = schedules.Where(s => s.DayOfWeek == day);
        }

        // Monday first: the school week. The day is stored as its name, so the order is spelled out (Sunday last).
        var page = await schedules
            .OrderBy(s => s.DayOfWeek == DayOfWeek.Monday ? 1
                : s.DayOfWeek == DayOfWeek.Tuesday ? 2
                : s.DayOfWeek == DayOfWeek.Wednesday ? 3
                : s.DayOfWeek == DayOfWeek.Thursday ? 4
                : s.DayOfWeek == DayOfWeek.Friday ? 5
                : s.DayOfWeek == DayOfWeek.Saturday ? 6
                : 7)
            .ThenBy(s => s.StartTime)
            .ThenBy(s => s.Classroom!.Name)
            .ThenBy(s => s.Id)
            .Select(ScheduleMapping.Projection)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<ScheduleDto>(page.Items.Select(ScheduleMapping.ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
