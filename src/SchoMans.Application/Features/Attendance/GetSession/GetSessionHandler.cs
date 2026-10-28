using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Attendance.GetSession;

/// <summary>A session with its students. Only the teacher of the lesson gets the QR code; admin staff and the principal see the list.</summary>
public sealed class GetSessionHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<AttendanceSessionDto>> Handle(GetSessionQuery query, CancellationToken ct)
    {
        var sessionId = query.Id;
        var teacherId = await db.AttendanceSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => (Guid?)s.Schedule!.TeacherId)
            .FirstOrDefaultAsync(ct);
        if (teacherId is null)
        {
            return Error.NotFound("attendance.session_not_found", "The attendance session does not exist.");
        }

        var (canView, isOwner) = await AttendanceSessions.AccessAsync(db, currentUser, teacherId.Value, ct);
        if (!canView)
        {
            return Error.Forbidden("attendance.not_your_lesson", "You can only see attendance of your own lessons.");
        }

        return (await AttendanceSessions.LoadAsync(db, sessionId, includeQr: isOwner, ct))!;
    }
}
