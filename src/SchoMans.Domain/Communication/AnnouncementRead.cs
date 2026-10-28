using SchoMans.Domain.Common;

namespace SchoMans.Domain.Communication;

/// <summary>Records that a user opened an announcement, for the unread state of the inbox.</summary>
public sealed class AnnouncementRead : BaseEntity
{
    private AnnouncementRead()
    {
    }

    public Guid AnnouncementId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset ReadAt { get; private set; }

    public static AnnouncementRead Create(Guid announcementId, Guid userId, DateTimeOffset readAt) =>
        new() { AnnouncementId = announcementId, UserId = userId, ReadAt = readAt };
}
