using SchoMans.Domain.Communication;

namespace SchoMans.Domain.Tests.Communication;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static Notification Create() =>
        Notification.Create(Guid.NewGuid(), NotificationType.Announcement, " Meeting ", " Tomorrow at 10 ", "/announcements/abc");

    [Fact]
    public void A_new_notification_is_unread()
    {
        var notification = Create();

        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
        Assert.Equal("Meeting", notification.Title);
        Assert.Equal("Tomorrow at 10", notification.Body);
    }

    [Fact]
    public void Marking_it_read_works_once_and_keeps_the_first_time()
    {
        var notification = Create();

        Assert.True(notification.MarkRead(Now));
        Assert.True(notification.IsRead);
        Assert.False(notification.MarkRead(Now.AddHours(1)));
        Assert.Equal(Now, notification.ReadAt);
    }

    [Fact]
    public void Requires_a_title_and_a_link()
    {
        Assert.ThrowsAny<ArgumentException>(() => Notification.Create(Guid.NewGuid(), NotificationType.Announcement, " ", "b", "/x"));
        Assert.ThrowsAny<ArgumentException>(() => Notification.Create(Guid.NewGuid(), NotificationType.Announcement, "t", "b", ""));
    }
}
