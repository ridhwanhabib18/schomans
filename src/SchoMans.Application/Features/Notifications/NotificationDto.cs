using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Notifications;

public sealed record NotificationDto(Guid Id, string Type, string Title, string Body, string LinkUrl, bool IsRead, DateTimeOffset CreatedAt);

internal static class NotificationMapping
{
    public static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type.ToString(), n.Title, n.Body, n.LinkUrl, n.IsRead, n.CreatedAt);
}
