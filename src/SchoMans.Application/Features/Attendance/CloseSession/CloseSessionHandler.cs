using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Attendance.CloseSession;

/// <summary>The teacher closes the session: the QR code stops working and everybody who did not scan is marked Absent.</summary>
public sealed class CloseSessionHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock, IAttendanceRealtime realtime)
{
    public async Task<Result<AttendanceSessionDto>> Handle(CloseSessionCommand command, CancellationToken ct)
    {
        var session = await db.AttendanceSessions.Include(s => s.Schedule).FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (session is null)
        {
            return Error.NotFound("attendance.session_not_found", "The attendance session does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != session.Schedule!.TeacherId)
        {
            return Error.Forbidden("attendance.not_your_lesson", "You can only close attendance of your own lessons.");
        }

        if (!await SessionCloser.CloseAsync(db, session, clock.UtcNow, ct))
        {
            return Error.Conflict("attendance.session_closed", "This attendance session is already closed.");
        }

        await db.SaveChangesAsync(ct);
        await realtime.SessionClosedAsync(session.Id, ct);
        return (await AttendanceSessions.LoadAsync(db, session.Id, includeQr: false, ct))!;
    }
}
