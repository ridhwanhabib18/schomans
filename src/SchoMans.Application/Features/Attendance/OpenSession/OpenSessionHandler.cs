using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;

namespace SchoMans.Application.Features.Attendance.OpenSession;

/// <summary>
/// The teacher opens attendance for a lesson of today. One session per lesson per day: opening an open one again returns
/// it (a second device, a reload); a closed one stays closed, the teacher corrects statuses by hand instead.
/// </summary>
public sealed class OpenSessionHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<AttendanceSessionDto>> Handle(OpenSessionCommand command, CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null)
        {
            return Error.Forbidden("attendance.not_a_teacher", "Only teachers take attendance.");
        }

        var schedule = await db.Schedules.AsNoTracking()
            .Where(s => s.Id == command.ScheduleId)
            .Select(s => new { s.Id, s.TeacherId, s.DayOfWeek, YearActive = s.Classroom!.AcademicYear!.IsActive })
            .FirstOrDefaultAsync(ct);
        if (schedule is null)
        {
            return Error.NotFound("schedules.not_found", "The lesson does not exist.");
        }

        if (schedule.TeacherId != teacherId)
        {
            return Error.Forbidden("attendance.not_your_lesson", "You can only take attendance for your own lessons.");
        }

        var today = clock.Today;
        if (schedule.DayOfWeek != today.DayOfWeek || !schedule.YearActive)
        {
            return Error.Validation("attendance.not_today", "This lesson is not on today's timetable.");
        }

        var existing = await db.AttendanceSessions.AsNoTracking().FirstOrDefaultAsync(s => s.ScheduleId == schedule.Id && s.Date == today, ct);
        if (existing is not null)
        {
            return existing.IsOpen
                ? (await AttendanceSessions.LoadAsync(db, existing.Id, includeQr: true, ct))!
                : Error.Conflict("attendance.session_already_closed", "Attendance for this lesson was already closed today. Correct statuses on the session instead.");
        }

        var session = AttendanceSession.Open(schedule.Id, today, clock.UtcNow);
        db.AttendanceSessions.Add(session);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two devices opened the same lesson at once: the unique index let one win; return that one.
            db.ChangeTracker.Clear();
            var winner = await db.AttendanceSessions.AsNoTracking().FirstAsync(s => s.ScheduleId == schedule.Id && s.Date == today, ct);
            return (await AttendanceSessions.LoadAsync(db, winner.Id, includeQr: true, ct))!;
        }

        return (await AttendanceSessions.LoadAsync(db, session.Id, includeQr: true, ct))!;
    }
}
