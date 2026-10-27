using SchoMans.Api.Hubs;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Announcements;
using SchoMans.Application.Features.Announcements.CreateAnnouncement;
using SchoMans.Domain.Students;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>"Announcements appear in real time": a published announcement reaches the browsers of its audience over SignalR.</summary>
[Collection(ApiCollection.Name)]
public class RealtimeNotificationTests(ApiFixture fixture)
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5); // the PRD target is under 5 seconds
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(800);

    private async Task<AnnouncementDto> PublishAsync(UserRole[] roles, string title)
    {
        using var admin = fixture.AsAdmin();
        var created = await (await admin.PostJsonAsync("/api/v1/announcements", new CreateAnnouncementCommand(title, "<p>Live update for you</p>", false, roles, []))).ReadAsync<AnnouncementDto>();
        (await admin.PostAsync($"/api/v1/announcements/{created.Id}/publish", null)).EnsureSuccessStatusCode();
        return created;
    }

    [Fact]
    public async Task A_connected_teacher_receives_the_announcement_the_moment_it_is_published()
    {
        await using var teacher = await HubTestClient.ConnectAsync(fixture.Factory, "teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");
        var title = $"Live {Guid.NewGuid():N}";

        var announcement = await PublishAsync([UserRole.Teacher], title);
        var pushed = await teacher.WaitForInvocationAsync(NotificationHub.NotificationCreatedEvent, Wait);

        Assert.NotNull(pushed);
        Assert.Equal(title, pushed.Value.GetProperty("title").GetString());
        Assert.Equal("Live update for you", pushed.Value.GetProperty("body").GetString());
        Assert.Equal($"/announcements/{announcement.Id}", pushed.Value.GetProperty("linkUrl").GetString());
        Assert.Equal("Announcement", pushed.Value.GetProperty("type").GetString());
        Assert.NotEqual(Guid.Empty, pushed.Value.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Somebody_outside_the_audience_gets_nothing()
    {
        await using var teacher = await HubTestClient.ConnectAsync(fixture.Factory, "teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");
        await using var student = await HubTestClient.ConnectAsync(fixture.Factory, "student@schomans.demo", "Rina Putri", "sub-demo-student", "Student");

        await PublishAsync([UserRole.Teacher], $"Teachers {Guid.NewGuid():N}");

        Assert.NotNull(await teacher.WaitForInvocationAsync(NotificationHub.NotificationCreatedEvent, Wait));
        Assert.Null(await student.WaitForInvocationAsync(NotificationHub.NotificationCreatedEvent, Quiet));
    }

    [Fact]
    public async Task Every_connection_of_the_same_user_receives_it()
    {
        await using var laptop = await HubTestClient.ConnectAsync(fixture.Factory, "teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");
        await using var phone = await HubTestClient.ConnectAsync(fixture.Factory, "teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");

        await PublishAsync([UserRole.Teacher], $"Two devices {Guid.NewGuid():N}");

        Assert.NotNull(await laptop.WaitForInvocationAsync(NotificationHub.NotificationCreatedEvent, Wait));
        Assert.NotNull(await phone.WaitForInvocationAsync(NotificationHub.NotificationCreatedEvent, Wait));
    }

    [Fact]
    public async Task Nobody_is_pushed_anything_for_a_draft()
    {
        await using var teacher = await HubTestClient.ConnectAsync(fixture.Factory, "teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");
        using var admin = fixture.AsAdmin();

        (await admin.PostJsonAsync("/api/v1/announcements", new CreateAnnouncementCommand($"Draft {Guid.NewGuid():N}", "<p>x</p>", false, [UserRole.Teacher], []))).EnsureSuccessStatusCode();

        Assert.Null(await teacher.WaitForInvocationAsync(NotificationHub.NotificationCreatedEvent, Quiet));
    }

    [Fact]
    public async Task The_hub_refuses_a_connection_without_a_token()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => HubTestClient.ConnectAnonymouslyAsync(fixture.Factory));
    }

    [Fact]
    public async Task The_hub_refuses_a_user_without_a_schomans_role()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HubTestClient.ConnectAsync(fixture.Factory, "norole@test.example", "No Role", $"sub-{Guid.NewGuid():N}", "offline_access"));
    }
}
