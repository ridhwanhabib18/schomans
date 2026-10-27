using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SchoMans.Api.Auth;
using SchoMans.Api.Middleware;

namespace SchoMans.Api.Hubs;

/// <summary>
/// Real-time channel for in-app notifications. A connection joins the group of its user (and of its role); the server
/// pushes <see cref="NotificationCreatedEvent"/> to the groups. Clients send nothing: the hub is receive-only.
/// </summary>
[Authorize(Policy = Policies.AnyRole)]
public sealed class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string NotificationCreatedEvent = "NotificationCreated";

    public static string UserGroup(Guid userId) => $"user:{userId}";

    public static string RoleGroup(string role) => $"role:{role}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(UserSyncMiddleware.UserIdClaim)?.Value;
        if (Guid.TryParse(userId, out var id))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(id));
        }

        var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (role is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role));
        }

        await base.OnConnectedAsync();
    }
}
