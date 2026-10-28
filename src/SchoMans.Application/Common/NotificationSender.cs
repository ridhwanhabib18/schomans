using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Common;

/// <summary>Stores one notification per recipient, then pushes them to whoever is online. Stored first: a failed push loses nothing.</summary>
internal static class NotificationSender
{
    public static async Task<int> SendAsync(
        IAppDbContext db,
        INotificationService notifications,
        IEnumerable<Guid> recipientUserIds,
        NotificationType type,
        string title,
        string body,
        string linkUrl,
        CancellationToken ct)
    {
        var stored = recipientUserIds
            .Distinct()
            .Select(userId => Notification.Create(userId, type, Shorten(title, 200), Shorten(body, 500), linkUrl))
            .ToList();
        if (stored.Count == 0)
        {
            return 0;
        }

        db.Notifications.AddRange(stored);
        await db.SaveChangesAsync(ct);

        await notifications.PushAsync(
            stored
                .Select(n => new UserNotification(n.UserId, new NotificationPush(n.Id, n.Type.ToString(), n.Title, n.Body, n.LinkUrl, n.CreatedAt)))
                .ToList(),
            ct);
        return stored.Count;
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
