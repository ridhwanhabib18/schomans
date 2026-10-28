using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Attendance.GetTodayLessons;

/// <summary>The teacher's lessons of today (school time), in order, each with its attendance session if one was opened.</summary>
public sealed class GetTodayLessonsHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<IReadOnlyList<TodayLessonDto>>> Handle(CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null)
        {
            return Error.Forbidden("attendance.not_a_teacher", "Only teachers take attendance.");
        }

        var today = clock.Today;
        var lessons = await db.Schedules.AsNoTracking()
            .Where(s => s.TeacherId == teacherId && s.DayOfWeek == today.DayOfWeek && s.Classroom!.AcademicYear!.IsActive)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id,
                s.ClassroomId,
                ClassroomName = s.Classroom!.Name,
                SubjectName = s.Subject!.Name,
                s.StartTime,
                s.EndTime,
                StudentCount = db.Students.Count(st => st.ClassroomId == s.ClassroomId && st.Status == StudentStatus.Active),
                Session = db.AttendanceSessions
                    .Where(a => a.ScheduleId == s.Id && a.Date == today)
                    .Select(a => new
                    {
                        a.Id,
                        Open = a.ClosedAt == null,
                        Present = db.AttendanceRecords.Count(r => r.SessionId == a.Id && r.Status == AttendanceStatus.Present),
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return lessons
            .Select(l => new TodayLessonDto(
                l.Id,
                l.ClassroomId,
                l.ClassroomName,
                l.SubjectName,
                l.StartTime,
                l.EndTime,
                l.Session?.Id,
                l.Session?.Open ?? false,
                l.Session?.Present ?? 0,
                l.StudentCount))
            .ToList();
    }
}
