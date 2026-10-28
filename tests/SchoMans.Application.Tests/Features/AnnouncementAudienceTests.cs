using SchoMans.Application.Features.Announcements;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Tests.Features;

/// <summary>The rule "who may read an announcement", checked on plain objects. The integration tests run the same rule as SQL.</summary>
public class AnnouncementAudienceTests
{
    private static readonly Guid ClassA = Guid.NewGuid();
    private static readonly Guid ClassB = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static Announcement Make(UserRole[] roles, Guid[] classrooms) =>
        Announcement.Create("Title", "<p>Body</p>", isPublic: false, roles, classrooms, CreatedBy);

    private static bool CanRead(Announcement announcement, UserRole role, params Guid[] userClassrooms) =>
        AnnouncementAudience.VisibleTo(role, userClassrooms).Compile()(announcement);

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Principal)]
    public void Admin_and_principal_read_every_announcement(UserRole role)
    {
        Assert.True(CanRead(Make([UserRole.Parent], [ClassB]), role));
        Assert.True(CanRead(Make([], []), role));
    }

    [Theory]
    [InlineData(UserRole.Principal)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Parent)]
    [InlineData(UserRole.Admin)]
    public void An_announcement_without_targets_is_for_every_school_role(UserRole role)
    {
        Assert.True(CanRead(Make([], []), role));
    }

    [Fact]
    public void An_applicant_is_not_part_of_the_school_so_an_untargeted_announcement_is_not_for_them()
    {
        Assert.False(CanRead(Make([], []), UserRole.Applicant));
    }

    [Fact]
    public void An_applicant_reads_an_announcement_that_names_the_applicant_role()
    {
        Assert.True(CanRead(Make([UserRole.Applicant], []), UserRole.Applicant));
    }

    [Fact]
    public void Only_the_named_roles_read_a_role_targeted_announcement()
    {
        var announcement = Make([UserRole.Teacher], []);

        Assert.True(CanRead(announcement, UserRole.Teacher));
        Assert.False(CanRead(announcement, UserRole.Student));
        Assert.False(CanRead(announcement, UserRole.Parent));
    }

    [Fact]
    public void A_classroom_targeted_announcement_reaches_the_members_of_that_classroom()
    {
        var announcement = Make([], [ClassA]);

        Assert.True(CanRead(announcement, UserRole.Student, ClassA));
        Assert.True(CanRead(announcement, UserRole.Parent, ClassA, ClassB)); // a parent of children in both classes
        Assert.True(CanRead(announcement, UserRole.Teacher, ClassA));        // the homeroom teacher
        Assert.False(CanRead(announcement, UserRole.Student, ClassB));
        Assert.False(CanRead(announcement, UserRole.Student));               // not in any classroom
    }

    [Fact]
    public void Roles_and_classrooms_must_both_match()
    {
        var announcement = Make([UserRole.Parent], [ClassA]);

        Assert.True(CanRead(announcement, UserRole.Parent, ClassA));
        Assert.False(CanRead(announcement, UserRole.Student, ClassA)); // right classroom, wrong role
        Assert.False(CanRead(announcement, UserRole.Parent, ClassB));  // right role, wrong classroom
    }
}
