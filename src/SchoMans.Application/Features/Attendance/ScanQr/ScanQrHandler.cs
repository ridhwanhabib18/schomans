using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Attendance.ScanQr;

/// <summary>
/// A student checks in by scanning the QR code on the classroom screen. The code is only valid for this session and
/// for about a minute, so a photo sent to a friend at home is useless. Scanning twice is harmless: the first record stays.
/// </summary>
public sealed class ScanQrHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ISchoolClock clock,
    IAttendanceRealtime realtime,
    IOptions<AttendanceOptions> options)
{
    public async Task<Result<ScanResultDto>> Handle(ScanQrCommand command, CancellationToken ct)
    {
        if (!AttendanceSession.TryParseToken(command.Token, out var sessionId, out var secret))
        {
            return Error.Validation("attendance.qr_invalid", "This is not a SchoMans attendance code.");
        }

        var student = await db.Students.AsNoTracking()
            .Where(s => s.UserId == currentUser.UserId)
            .Select(s => new { s.Id, s.ClassroomId, s.Status, s.Nisn, s.User!.FullName })
            .FirstOrDefaultAsync(ct);
        if (student is null)
        {
            return Error.Forbidden("attendance.not_a_student", "Only students check in.");
        }

        var session = await db.AttendanceSessions
            .Where(s => s.Id == sessionId)
            .Select(s => new { Session = s, s.Schedule!.ClassroomId, ClassroomName = s.Schedule.Classroom!.Name, SubjectName = s.Schedule.Subject!.Name })
            .FirstOrDefaultAsync(ct);
        if (session is null)
        {
            return Error.Validation("attendance.qr_invalid", "This is not a SchoMans attendance code.");
        }

        if (session.ClassroomId != student.ClassroomId || student.Status != StudentStatus.Active)
        {
            return Error.Forbidden("attendance.not_in_classroom", "This attendance code is for another classroom.");
        }

        var existing = await db.AttendanceRecords.AsNoTracking().FirstOrDefaultAsync(r => r.SessionId == sessionId && r.StudentId == student.Id, ct);
        if (existing is not null)
        {
            // A second scan, or the teacher already marked the student: report what is recorded, change nothing.
            return new ScanResultDto(sessionId, session.ClassroomName, session.SubjectName, existing.Status, existing.ScannedAt, AlreadyRecorded: true);
        }

        if (!session.Session.IsOpen)
        {
            return Error.Conflict("attendance.session_closed", "Attendance for this lesson is already closed. Ask your teacher.");
        }

        var now = clock.UtcNow;
        if (!session.Session.AcceptsSecret(secret, now))
        {
            return Error.Validation("attendance.qr_expired", "This code has expired. Scan the code that is on the screen now.");
        }

        var fence = options.Value.Geofence;
        if (fence.Enabled)
        {
            if (command.Latitude is not { } latitude || command.Longitude is not { } longitude)
            {
                return Error.Validation("attendance.location_required", "Allow location access to check in: attendance only counts at school.");
            }

            if (!Geofence.IsInside(fence, latitude, longitude))
            {
                return Error.Validation("attendance.outside_school", "You are not at school. Check in from the classroom.");
            }
        }

        var record = AttendanceRecord.Scanned(sessionId, student.Id, now);
        db.AttendanceRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same scan arrived twice at the same moment; the unique index kept the first.
            db.ChangeTracker.Clear();
            var first = await db.AttendanceRecords.AsNoTracking().FirstAsync(r => r.SessionId == sessionId && r.StudentId == student.Id, ct);
            return new ScanResultDto(sessionId, session.ClassroomName, session.SubjectName, first.Status, first.ScannedAt, AlreadyRecorded: true);
        }

        await realtime.StudentCheckedInAsync(sessionId, AttendanceSessions.ToStudentDto(record, student.FullName, student.Nisn), ct);
        return new ScanResultDto(sessionId, session.ClassroomName, session.SubjectName, record.Status, record.ScannedAt, AlreadyRecorded: false);
    }
}
