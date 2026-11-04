using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Common;

/// <summary>
/// Stores one notification per recipient, then pushes them to whoever is online. Stored first: a failed push loses nothing.
/// With <c>alsoEmail</c> the same message is also queued as an email in the same save (F-11); a job sends the queue.
/// </summary>
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
        CancellationToken ct,
        bool alsoEmail = false)
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
        if (alsoEmail)
        {
            await QueueEmailsAsync(db, stored.Select(n => n.UserId).ToList(), title, body, linkUrl, ct);
        }

        await db.SaveChangesAsync(ct);

        await notifications.PushAsync(
            stored
                .Select(n => new UserNotification(n.UserId, new NotificationPush(n.Id, n.Type.ToString(), n.Title, n.Body, n.LinkUrl, n.CreatedAt)))
                .ToList(),
            ct);
        return stored.Count;
    }

    /// <summary>Adds one queued email per recipient that has an address. The caller saves.</summary>
    public static async Task QueueEmailsAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> userIds, string subject, string body, string linkUrl, CancellationToken ct)
    {
        var addresses = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.Email != "")
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);
        db.EmailMessages.AddRange(addresses.Select(a => EmailMessage.Create(a.Id, a.Email, subject, body, linkUrl)));
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
