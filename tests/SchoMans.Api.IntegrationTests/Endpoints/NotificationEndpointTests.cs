using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Announcements;
using SchoMans.Application.Features.Announcements.CreateAnnouncement;
using SchoMans.Application.Features.Notifications;
using SchoMans.Application.Features.Notifications.GetUnreadNotificationCount;
using SchoMans.Domain.Students;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class NotificationEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/notifications";

    private async Task PublishToAsync(UserRole role, string title)
    {
        using var admin = fixture.AsAdmin();
        var created = await (await admin.PostJsonAsync("/api/v1/announcements", new CreateAnnouncementCommand(title, "<p>Body of the notice</p>", false, [role], []))).ReadAsync<AnnouncementDto>();
        (await admin.PostAsync($"/api/v1/announcements/{created.Id}/publish", null)).EnsureSuccessStatusCode();
    }

    private static async Task<int> UnreadAsync(HttpClient client) =>
        (await (await client.GetAsync($"{Url}/unread-count")).ReadAsync<UnreadNotificationCountDto>()).Count;

    [Fact]
    public async Task Published_announcements_arrive_as_unread_notifications_with_a_link_and_an_excerpt()
    {
        using var teacher = fixture.AsTeacher();
        var title = $"Staff notice {Guid.NewGuid():N}";
        var before = await UnreadAsync(teacher);

        await PublishToAsync(UserRole.Teacher, title);

        Assert.Equal(before + 1, await UnreadAsync(teacher));
        var page = await (await teacher.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<NotificationDto>>();
        var notification = page.Items.Single(n => n.Title == title);
        Assert.False(notification.IsRead);
        Assert.Equal("Announcement", notification.Type);
        Assert.Equal("Body of the notice", notification.Body);
        Assert.StartsWith("/announcements/", notification.LinkUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Everybody_only_sees_their_own_notifications()
    {
        using var teacher = fixture.AsTeacher();
        using var student = fixture.AsStudent();
        var title = $"Teachers only {Guid.NewGuid():N}";

        await PublishToAsync(UserRole.Teacher, title);

        var theirs = await (await student.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<NotificationDto>>();
        Assert.DoesNotContain(theirs.Items, n => n.Title == title);
    }

    [Fact]
    public async Task The_newest_notification_comes_first_and_the_list_can_show_unread_only()
    {
        using var teacher = fixture.AsTeacher();
        var firstTitle = $"First {Guid.NewGuid():N}";
        var secondTitle = $"Second {Guid.NewGuid():N}";
        await PublishToAsync(UserRole.Teacher, firstTitle);
        await Task.Delay(30);
        await PublishToAsync(UserRole.Teacher, secondTitle);
        var all = await (await teacher.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<NotificationDto>>();
        var first = all.Items.Single(n => n.Title == firstTitle);
        (await teacher.PostAsync($"{Url}/{first.Id}/read", null)).EnsureSuccessStatusCode();

        var after = await (await teacher.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<NotificationDto>>();
        var unread = await (await teacher.GetAsync($"{Url}?pageSize=100&unreadOnly=true")).ReadAsync<PagedResult<NotificationDto>>();

        var titles = after.Items.Select(n => n.Title).ToList();
        Assert.True(titles.IndexOf(secondTitle) < titles.IndexOf(firstTitle));
        Assert.DoesNotContain(unread.Items, n => n.Title == firstTitle);
        Assert.Contains(unread.Items, n => n.Title == secondTitle);
    }

    [Fact]
    public async Task Marking_one_notification_read_lowers_the_unread_count_once()
    {
        using var teacher = fixture.AsTeacher();
        await PublishToAsync(UserRole.Teacher, $"Count {Guid.NewGuid():N}");
        var before = await UnreadAsync(teacher);
        var latest = (await (await teacher.GetAsync($"{Url}?pageSize=1&unreadOnly=true")).ReadAsync<PagedResult<NotificationDto>>()).Items.Single();

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync($"{Url}/{latest.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync($"{Url}/{latest.Id}/read", null)).StatusCode);

        Assert.Equal(before - 1, await UnreadAsync(teacher));
    }

    [Fact]
    public async Task Somebody_elses_notification_is_not_found_and_stays_unread()
    {
        using var teacher = fixture.AsTeacher();
        using var student = fixture.AsStudent();
        await PublishToAsync(UserRole.Teacher, $"Private {Guid.NewGuid():N}");
        var theirs = (await (await teacher.GetAsync($"{Url}?pageSize=1&unreadOnly=true")).ReadAsync<PagedResult<NotificationDto>>()).Items.Single();
        var before = await UnreadAsync(teacher);

        var response = await student.PostAsync($"{Url}/{theirs.Id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await UnreadAsync(teacher));
    }

    [Fact]
    public async Task Mark_all_read_clears_the_badge()
    {
        using var teacher = fixture.AsTeacher();
        await PublishToAsync(UserRole.Teacher, $"One {Guid.NewGuid():N}");
        await PublishToAsync(UserRole.Teacher, $"Two {Guid.NewGuid():N}");
        Assert.True(await UnreadAsync(teacher) >= 2);

        var response = await teacher.PostAsync($"{Url}/read-all", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await UnreadAsync(teacher));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    public async Task The_list_is_validated_like_every_other_list(string query)
    {
        using var teacher = fixture.AsTeacher();

        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.GetAsync($"{Url}?{query}")).StatusCode);
    }

    [Fact]
    public async Task Notifications_need_a_signed_in_user_with_a_role()
    {
        using var guest = fixture.Factory.CreateClient();
        using var noRole = fixture.AsNewUser("offline_access");

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noRole.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsync($"{Url}/read-all", null)).StatusCode);
    }
}
