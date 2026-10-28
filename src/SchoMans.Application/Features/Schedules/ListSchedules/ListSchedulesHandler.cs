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

        // Monday first: the school week. Sunday (0 in .NET) goes last.
        var page = await schedules
            .OrderBy(s => s.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ThenBy(s => s.Classroom!.Name)
            .ThenBy(s => s.Id)
            .Select(ScheduleMapping.Projection)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<ScheduleDto>(page.Items.Select(ScheduleMapping.ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
