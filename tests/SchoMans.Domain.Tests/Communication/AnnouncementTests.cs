using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Tests.Communication;

public class AnnouncementTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Author = Guid.NewGuid();

    private static Announcement Draft() =>
        Announcement.Create("Meeting", "<p>Tomorrow</p>", isPublic: false, [UserRole.Teacher, UserRole.Teacher], [], Author);

    [Fact]
    public void A_new_announcement_is_a_draft_and_its_targets_have_no_duplicates()
    {
        var announcement = Draft();

        Assert.Equal(PublishStatus.Draft, announcement.Status);
        Assert.Equal([UserRole.Teacher], announcement.TargetRoles);
        Assert.Empty(announcement.TargetClassroomIds);
        Assert.Equal(Author, announcement.CreatedById);
    }

    [Fact]
    public void The_audience_can_change_while_it_is_a_draft()
    {
        var announcement = Draft();
        var classroom = Guid.NewGuid();

        var changed = announcement.UpdateTargets(isPublic: true, [UserRole.Parent], [classroom, classroom]);

        Assert.True(changed);
        Assert.True(announcement.IsPublic);
        Assert.Equal([UserRole.Parent], announcement.TargetRoles);
        Assert.Equal([classroom], announcement.TargetClassroomIds);
    }

    [Fact]
    public void The_audience_is_locked_once_published_because_the_notifications_have_gone_out()
    {
        var announcement = Draft();
        announcement.Publish(Now);

        var changed = announcement.UpdateTargets(isPublic: true, [UserRole.Parent], []);

        Assert.False(changed);
        Assert.False(announcement.IsPublic);
        Assert.Equal([UserRole.Teacher], announcement.TargetRoles);
    }

    [Fact]
    public void The_text_can_still_be_corrected_after_publishing()
    {
        var announcement = Draft();
        announcement.Publish(Now);

        announcement.UpdateContent("Meeting (moved)", "<p>Thursday</p>");

        Assert.Equal("Meeting (moved)", announcement.Title);
        Assert.Equal(PublishStatus.Published, announcement.Status);
    }

    [Fact]
    public void Publishing_works_once()
    {
        var announcement = Draft();

        Assert.True(announcement.Publish(Now));
        Assert.Equal(Now, announcement.PublishedAt);
        Assert.False(announcement.Publish(Now.AddHours(1)));
        Assert.Equal(Now, announcement.PublishedAt);
    }

    [Fact]
    public void Requires_a_title()
    {
        Assert.ThrowsAny<ArgumentException>(() => Announcement.Create(" ", "<p>x</p>", false, [], [], Author));
    }
}
