using Microsoft.AspNetCore.SignalR;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Api.Hubs;

/// <summary>Pushes stored notifications to the browsers of their users. A failure is logged and swallowed: nothing is lost, the notification is stored.</summary>
public sealed class SignalRNotificationService(IHubContext<NotificationHub> hub, ILogger<SignalRNotificationService> logger) : INotificationService
{
    public async Task PushAsync(IReadOnlyCollection<UserNotification> notifications, CancellationToken ct)
    {
        foreach (var notification in notifications)
        {
            try
            {
                await hub.Clients.Group(NotificationHub.UserGroup(notification.UserId))
                    .SendAsync(NotificationHub.NotificationCreatedEvent, notification.Payload, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not push notification {NotificationId} to user {UserId}", notification.Payload.Id, notification.UserId);
            }
        }
    }
}
