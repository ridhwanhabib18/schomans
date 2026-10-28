using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Attendance.CloseSession;

namespace SchoMans.Application.Features.Attendance.CloseStaleSessions;

/// <summary>
/// Run every 15 minutes: closes sessions a teacher forgot to close, once the lesson is over (plus a grace period), and
/// marks the students who did not scan Absent. A session opened late always stays open for a minimum time first.
/// </summary>
public sealed class CloseStaleSessionsHandler(IAppDbContext db, ISchoolClock clock, IAttendanceRealtime realtime, IOptions<AttendanceOptions> options)
{
    /// <returns>How many sessions were closed.</returns>
    public async Task<int> Handle(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var settings = options.Value;
        var open = await db.AttendanceSessions.Include(s => s.Schedule).Where(s => s.ClosedAt == null).ToListAsync(ct);

        var stale = open
            .Where(s => now >= clock.ToUtc(s.Date, s.Schedule!.EndTime).AddMinutes(settings.CloseAfterLessonMinutes)
                && now >= s.OpenedAt.AddMinutes(settings.MinimumOpenMinutes))
            .ToList();

        foreach (var session in stale)
        {
            await SessionCloser.CloseAsync(db, session, now, ct);
        }

        if (stale.Count == 0)
        {
            return 0;
        }

        await db.SaveChangesAsync(ct);
        foreach (var session in stale)
        {
            await realtime.SessionClosedAsync(session.Id, ct);
        }

        return stale.Count;
    }
}
