using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SchoMans.Api.Auth;
using SchoMans.Api.Middleware;
using SchoMans.Application.Features.Attendance.GetSession;
using SchoMans.Application.Features.Users.SyncUser;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Identity;

namespace SchoMans.Api.Hubs;

/// <summary>
/// Live attendance for the session screen. The screen calls <see cref="JoinSession"/>; the server checks that the user
/// may see the session and then pushes check-ins and closing to the session's group. The QR codes go only to the group
/// of the teacher who runs the session, never to other viewers.
/// </summary>
[Authorize(Policy = Policies.StaffRead)]
public sealed class AttendanceHub(GetSessionHandler sessions, CurrentUser currentUser) : Hub
{
    public const string Path = "/hubs/attendance";
    public const string StudentCheckedInEvent = "StudentCheckedIn";
    public const string QrRotatedEvent = "QrRotated";
    public const string SessionClosedEvent = "SessionClosed";

    public static string SessionGroup(Guid sessionId) => $"session:{sessionId}";

    public static string SessionOwnerGroup(Guid sessionId) => $"session:{sessionId}:owner";

    public async Task JoinSession(Guid sessionId)
    {
        FillCurrentUser();
        var session = await sessions.Handle(new GetSessionQuery(sessionId), Context.ConnectionAborted);
        if (session.IsFailure)
        {
            throw new HubException(session.Error.Code);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);
        if (session.Value.Qr is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SessionOwnerGroup(sessionId), Context.ConnectionAborted);
        }
    }

    public async Task LeaveSession(Guid sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionOwnerGroup(sessionId));
    }

    // A hub method runs in its own DI scope, where the request middleware did not fill the current user. The claims of
    // the connection hold everything: the middleware added the database id to them when the connection was opened.
    private void FillCurrentUser()
    {
        var user = Context.User ?? throw new HubException("auth.required");
        if (!Guid.TryParse(user.FindFirstValue(UserSyncMiddleware.UserIdClaim), out var userId)
            || !Enum.TryParse<UserRole>(user.FindFirstValue(ClaimTypes.Role), out var role))
        {
            throw new HubException("auth.incomplete_token");
        }

        currentUser.Set(new SyncedUserDto(
            userId,
            user.FindFirstValue("sub") ?? string.Empty,
            KeycloakClaims.GetEmail(user) ?? string.Empty,
            KeycloakClaims.GetFullName(user) ?? string.Empty,
            role));
    }
}
