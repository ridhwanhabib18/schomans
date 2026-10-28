namespace SchoMans.Application.Common.Interfaces;

/// <summary>The payload pushed to the browser; it matches a stored Notification.</summary>
public sealed record NotificationPush(Guid Id, string Type, string Title, string Body, string LinkUrl, DateTimeOffset CreatedAt);

public sealed record UserNotification(Guid UserId, NotificationPush Payload);

/// <summary>Pushes notifications to connected browsers in real time (SignalR). The notifications are already stored; a failed push loses nothing.</summary>
public interface INotificationService
{
    Task PushAsync(IReadOnlyCollection<UserNotification> notifications, CancellationToken ct);
}
