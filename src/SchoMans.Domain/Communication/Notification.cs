using SchoMans.Domain.Common;

namespace SchoMans.Domain.Communication;

/// <summary>An in-app notification for one user. Unread ones drive the badge; new ones are also pushed in real time.</summary>
public sealed class Notification : BaseEntity
{
    private Notification()
    {
    }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>The app path to open when the notification is clicked, for example <c>/announcements/{id}</c>.</summary>
    public string LinkUrl { get; private set; } = string.Empty;

    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(Guid userId, NotificationType type, string title, string body, string linkUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(linkUrl);

        return new Notification { UserId = userId, Type = type, Title = title.Trim(), Body = body.Trim(), LinkUrl = linkUrl };
    }

    /// <summary>Marks the notification as read. Returns false when it already was.</summary>
    public bool MarkRead(DateTimeOffset now)
    {
        if (IsRead)
        {
            return false;
        }

        ReadAt = now;
        return true;
    }
}
