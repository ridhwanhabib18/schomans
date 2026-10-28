using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Attendance.MarkAttendance;

/// <summary>
/// The teacher of the lesson sets one student's status, while the session is open or afterwards (a letter from home the
/// next day). It also works for a student who has no record yet.
/// </summary>
public sealed class MarkAttendanceHandler(IAppDbContext db, ICurrentUser currentUser, IAttendanceRealtime realtime)
{
    public async Task<Result<SessionStudentDto>> Handle(MarkAttendanceCommand command, CancellationToken ct)
    {
        var session = await db.AttendanceSessions.AsNoTracking()
            .Where(s => s.Id == command.SessionId)
            .Select(s => new { s.Id, s.Schedule!.TeacherId, s.Schedule.ClassroomId, Open = s.ClosedAt == null })
            .FirstOrDefaultAsync(ct);
        if (session is null)
        {
            return Error.NotFound("attendance.session_not_found", "The attendance session does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != session.TeacherId)
        {
            return Error.Forbidden("attendance.not_your_lesson", "You can only change attendance of your own lessons.");
        }

        var record = await db.AttendanceRecords.FirstOrDefaultAsync(r => r.SessionId == session.Id && r.StudentId == command.StudentId, ct);
        var student = await db.Students.AsNoTracking()
            .Where(s => s.Id == command.StudentId)
            .Select(s => new { s.ClassroomId, s.Status, s.Nisn, s.User!.FullName })
            .FirstOrDefaultAsync(ct);
        if (student is null || (record is null && (student.ClassroomId != session.ClassroomId || student.Status != StudentStatus.Active)))
        {
            return Error.NotFound("attendance.student_not_in_session", "The student is not in this lesson.");
        }

        if (record is null)
        {
            record = AttendanceRecord.Marked(session.Id, command.StudentId, command.Status, currentUser.UserId, command.Note);
            db.AttendanceRecords.Add(record);
        }
        else
        {
            record.Mark(command.Status, currentUser.UserId, command.Note);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The student scanned at the same moment; set the status on that record instead.
            db.ChangeTracker.Clear();
            record = await db.AttendanceRecords.FirstAsync(r => r.SessionId == session.Id && r.StudentId == command.StudentId, ct);
            record.Mark(command.Status, currentUser.UserId, command.Note);
            await db.SaveChangesAsync(ct);
        }

        var dto = AttendanceSessions.ToStudentDto(record, student.FullName, student.Nisn);
        if (session.Open)
        {
            await realtime.StudentCheckedInAsync(session.Id, dto, ct);
        }

        return dto;
    }
}
