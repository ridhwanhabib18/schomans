using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements.PublishAnnouncement;

/// <summary>
/// Publishes a draft and notifies its audience: one stored notification per recipient (they drive the unread badge),
/// then a real-time push for everybody who is online.
/// </summary>
public sealed class PublishAnnouncementHandler(
    IAppDbContext db,
    IFileStorage storage,
    IHtmlContentSanitizer sanitizer,
    INotificationService notifications,
    TimeProvider timeProvider)
{
    public async Task<Result<PublishAnnouncementResult>> Handle(PublishAnnouncementCommand command, CancellationToken ct)
    {
        var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == command.Id, ct);
        if (announcement is null)
        {
            return Error.NotFound("announcements.not_found", "The announcement does not exist.");
        }

        var now = timeProvider.GetUtcNow();
        if (!announcement.Publish(now))
        {
            return Error.Conflict("announcements.already_published", "The announcement is already published.");
        }

        var recipientIds = await AnnouncementAudience.RecipientUserIdsAsync(db, announcement, ct);
        var excerpt = Features.News.NewsMapping.Excerpt(sanitizer.ToPlainText(announcement.Body));
        var link = $"/announcements/{announcement.Id}";

        var stored = recipientIds
            .Select(userId => Notification.Create(userId, NotificationType.Announcement, announcement.Title, excerpt, link))
            .ToList();
        db.Notifications.AddRange(stored);
        await db.SaveChangesAsync(ct);

        // Stored first, pushed second: a failed push loses nothing, the notification is there at the next page load.
        await notifications.PushAsync(
            stored
                .Select(n => new UserNotification(
                    n.UserId,
                    new NotificationPush(n.Id, n.Type.ToString(), n.Title, n.Body, n.LinkUrl, n.CreatedAt)))
                .ToList(),
            ct);

        return new PublishAnnouncementResult(AnnouncementMapping.ToDto(announcement, storage), stored.Count);
    }
}
