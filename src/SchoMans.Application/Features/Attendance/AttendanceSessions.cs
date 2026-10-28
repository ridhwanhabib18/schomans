using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Attendance;

/// <summary>Loading a session for the screen, and who may see it.</summary>
internal static class AttendanceSessions
{
    /// <summary>How often the QR code changes (the recurring job runs every minute).</summary>
    public static readonly TimeSpan RotationInterval = TimeSpan.FromMinutes(1);

    public static QrCodeDto QrOf(AttendanceSession session) =>
        new(session.QrToken, session.QrRotatedAt, session.QrRotatedAt + RotationInterval);

    /// <summary>The teacher of the lesson runs the session; admin staff and the principal may look at it.</summary>
    public static async Task<(bool CanView, bool IsOwner)> AccessAsync(IAppDbContext db, ICurrentUser user, Guid scheduleTeacherId, CancellationToken ct)
    {
        if (user.Role is UserRole.Admin or UserRole.Principal)
        {
            return (true, false);
        }

        if (user.Role != UserRole.Teacher)
        {
            return (false, false);
        }

        var teacherId = await SchoolMembership.TeacherIdAsync(db, user, ct);
        var isOwner = teacherId == scheduleTeacherId;
        return (isOwner, isOwner);
    }

    public static async Task<AttendanceSessionDto?> LoadAsync(IAppDbContext db, Guid sessionId, bool includeQr, CancellationToken ct)
    {
        var session = await db.AttendanceSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new
            {
                Session = s,
                s.Schedule!.ClassroomId,
                ClassroomName = s.Schedule.Classroom!.Name,
                SubjectName = s.Schedule.Subject!.Name,
                TeacherName = s.Schedule.Teacher!.User!.FullName,
                s.Schedule.StartTime,
                s.Schedule.EndTime,
            })
            .FirstOrDefaultAsync(ct);
        if (session is null)
        {
            return null;
        }

        var students = await StudentsAsync(db, session.Session, session.ClassroomId, ct);
        return new AttendanceSessionDto(
            session.Session.Id,
            session.Session.ScheduleId,
            session.ClassroomId,
            session.ClassroomName,
            session.SubjectName,
            session.TeacherName,
            session.Session.Date,
            session.StartTime,
            session.EndTime,
            session.Session.IsOpen,
            session.Session.OpenedAt,
            session.Session.ClosedAt,
            includeQr && session.Session.IsOpen ? QrOf(session.Session) : null,
            students);
    }

    /// <summary>
    /// The students of the session: everybody who has a record, plus, while the session is open, the active students of
    /// the classroom who have not scanned yet. Ordered by name.
    /// </summary>
    public static async Task<List<SessionStudentDto>> StudentsAsync(IAppDbContext db, AttendanceSession session, Guid classroomId, CancellationToken ct)
    {
        var recorded = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.SessionId == session.Id)
            .Select(r => new SessionStudentDto(r.StudentId, r.Student!.User!.FullName, r.Student.Nisn, r.Status, r.ScannedAt, r.IsManual, r.Note))
            .ToListAsync(ct);

        if (session.IsOpen)
        {
            var recordedIds = recorded.Select(r => r.StudentId).ToList();
            var waiting = await db.Students.AsNoTracking()
                .Where(s => s.ClassroomId == classroomId && s.Status == StudentStatus.Active && !recordedIds.Contains(s.Id))
                .Select(s => new SessionStudentDto(s.Id, s.User!.FullName, s.Nisn, null, null, false, null))
                .ToListAsync(ct);
            recorded.AddRange(waiting);
        }

        return recorded.OrderBy(s => s.FullName, StringComparer.CurrentCultureIgnoreCase).ThenBy(s => s.Nisn).ToList();
    }

    public static SessionStudentDto ToStudentDto(AttendanceRecord record, string fullName, string nisn) =>
        new(record.StudentId, fullName, nisn, record.Status, record.ScannedAt, record.IsManual, record.Note);
}
