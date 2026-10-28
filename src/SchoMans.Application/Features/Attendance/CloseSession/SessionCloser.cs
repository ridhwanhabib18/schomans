using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Attendance.CloseSession;

/// <summary>Closing a session, shared by the teacher's button and the clean-up job: everybody who did not scan becomes Absent.</summary>
internal static class SessionCloser
{
    /// <summary>Closes the session and records the missing students as Absent. Returns false when it was already closed.</summary>
    public static async Task<bool> CloseAsync(IAppDbContext db, AttendanceSession session, DateTimeOffset now, CancellationToken ct)
    {
        if (!session.Close(now))
        {
            return false;
        }

        var classroomId = await db.Schedules.Where(s => s.Id == session.ScheduleId).Select(s => s.ClassroomId).FirstAsync(ct);
        var missing = await db.Students
            .Where(s => s.ClassroomId == classroomId
                && s.Status == StudentStatus.Active
                && !db.AttendanceRecords.Any(r => r.SessionId == session.Id && r.StudentId == s.Id))
            .Select(s => s.Id)
            .ToListAsync(ct);

        db.AttendanceRecords.AddRange(missing.Select(studentId => AttendanceRecord.MissedScan(session.Id, studentId)));
        return true;
    }
}
