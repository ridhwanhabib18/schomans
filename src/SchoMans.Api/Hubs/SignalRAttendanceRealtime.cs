using Microsoft.AspNetCore.SignalR;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Attendance;

namespace SchoMans.Api.Hubs;

/// <summary>Pushes attendance events to the session screens. A failure is logged and swallowed: the change is saved, the screen reloads it.</summary>
public sealed class SignalRAttendanceRealtime(IHubContext<AttendanceHub> hub, ILogger<SignalRAttendanceRealtime> logger) : IAttendanceRealtime
{
    public Task StudentCheckedInAsync(Guid sessionId, SessionStudentDto student, CancellationToken ct) =>
        SendAsync(AttendanceHub.SessionGroup(sessionId), AttendanceHub.StudentCheckedInEvent, student, ct);

    public Task QrRotatedAsync(Guid sessionId, QrCodeDto qr, CancellationToken ct) =>
        SendAsync(AttendanceHub.SessionOwnerGroup(sessionId), AttendanceHub.QrRotatedEvent, qr, ct);

    public Task SessionClosedAsync(Guid sessionId, CancellationToken ct) =>
        SendAsync(AttendanceHub.SessionGroup(sessionId), AttendanceHub.SessionClosedEvent, sessionId, ct);

    private async Task SendAsync(string group, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            await hub.Clients.Group(group).SendAsync(eventName, payload, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not push {Event} to {Group}", eventName, group);
        }
    }
}
