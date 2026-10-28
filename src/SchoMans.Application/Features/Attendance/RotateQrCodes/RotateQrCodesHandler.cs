using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Attendance.RotateQrCodes;

/// <summary>Run every minute by a recurring job: every open session gets a new QR secret, and its screen gets the new code.</summary>
public sealed class RotateQrCodesHandler(IAppDbContext db, ISchoolClock clock, IAttendanceRealtime realtime)
{
    /// <returns>How many sessions were rotated.</returns>
    public async Task<int> Handle(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var sessions = await db.AttendanceSessions.Where(s => s.ClosedAt == null).ToListAsync(ct);
        foreach (var session in sessions)
        {
            session.RotateQr(now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A teacher closed a session at the same moment; the next run rotates the others again.
            return 0;
        }

        foreach (var session in sessions)
        {
            await realtime.QrRotatedAsync(session.Id, AttendanceSessions.QrOf(session), ct);
        }

        return sessions.Count;
    }
}
