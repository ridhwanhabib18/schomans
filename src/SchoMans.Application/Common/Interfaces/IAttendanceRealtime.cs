using SchoMans.Application.Features.Attendance;

namespace SchoMans.Application.Common.Interfaces;

/// <summary>
/// Live updates for the teacher's session screen (SignalR group of the session). Every event follows a saved change, so a
/// failed push loses nothing: the screen also reloads the session from the API.
/// </summary>
public interface IAttendanceRealtime
{
    Task StudentCheckedInAsync(Guid sessionId, SessionStudentDto student, CancellationToken ct);

    Task QrRotatedAsync(Guid sessionId, QrCodeDto qr, CancellationToken ct);

    Task SessionClosedAsync(Guid sessionId, CancellationToken ct);
}
