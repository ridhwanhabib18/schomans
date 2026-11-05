using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Announcements;
using SchoMans.Application.Features.Announcements.CreateAnnouncement;
using SchoMans.Application.Features.Chatbot;
using SchoMans.Application.Features.News;
using SchoMans.Application.Features.News.CreateNews;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// The test set of AI-08 (school and admissions chatbot): answers come only from published sources and link to them; when the sources
/// do not contain the answer the bot says so with the staff contact; the public widget never sees what is not public. The AI provider is the mock.
/// </summary>
[Collection(ApiCollection.Name)]
public class ChatbotEndpointTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string Public = "/api/v1/chatbot/public";
    private const string Parent = "/api/v1/chatbot/parent";

    // The shared host allows 6 questions a minute from one address; these tests ask more, so they run on a host with a wide allowance.
    private ApiFactory _open = null!;

    public Task InitializeAsync()
    {
        _open = fixture.CreateFactory(new Dictionary<string, string> { ["RateLimiting:ChatbotPerMinute"] = "1000" });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _open.DisposeAsync();

    /// <summary>A word no other test uses, so only what a test publishes can be found.</summary>
    private static string Token() => "qx" + Guid.NewGuid().ToString("N");

    private HttpClient Editor() => _open.AsUser("admin@schomans.demo", "M Ridhwan Habib A", "sub-demo-admin", "Admin");

    private static async Task<NewsDto> NewsAsync(HttpClient editor, string title, string body, NewsCategory category = NewsCategory.News, bool publish = true)
    {
        var created = await (await editor.PostJsonAsync("/api/v1/news", new CreateNewsCommand(title, body, category, null))).ReadAsync<NewsDto>();
        if (publish)
        {
            (await editor.PostAsync($"/api/v1/news/{created.Id}/publish", null)).EnsureSuccessStatusCode();
        }

        return created;
    }

    private static async Task<AnnouncementDto> AnnouncementAsync(HttpClient editor, string title, string body, bool isPublic, UserRole[]? roles = null, Guid[]? classrooms = null, bool publish = true)
    {
        var created = await (await editor.PostJsonAsync("/api/v1/announcements", new CreateAnnouncementCommand(title, body, isPublic, roles ?? [], classrooms ?? []))).ReadAsync<AnnouncementDto>();
        if (publish)
        {
            (await editor.PostAsync($"/api/v1/announcements/{created.Id}/publish", null)).EnsureSuccessStatusCode();
        }

        return created;
    }

    private static async Task<ChatbotAnswerDto> AskAsync(HttpClient client, string url, string question)
    {
        var response = await client.PostJsonAsync(url, new AskChatbotCommand(question));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<ChatbotAnswerDto>();
    }

    private async Task<int> ChatbotCallsAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiUsageLogs.CountAsync(l => l.Feature == "AI-08");
    }

    // ---- answers from the sources ----

    [Fact]
    public async Task A_guest_gets_an_answer_from_a_published_article_with_a_link_to_it()
    {
        var token = Token();
        using var admin = Editor();
        var article = await NewsAsync(admin, $"Registration {token}", $"<p>Registration for {token} opens on 1 March. Bring the birth certificate.</p>");
        using var guest = _open.CreateClient();

        var answer = await AskAsync(guest, Public, $"When does registration for {token} open?");

        Assert.True(answer.Known);
        Assert.Contains("opens on 1 March", answer.Answer);
        Assert.Null(answer.Contact);
        var source = Assert.Single(answer.Sources);
        Assert.Equal(($"Registration {token}", $"/news/{article.Slug}"), (source.Title, source.Url));
    }

    [Fact]
    public async Task Admission_information_is_a_source_too()
    {
        var token = Token();
        using var admin = Editor();
        await NewsAsync(admin, $"Admission {token}", $"<p>The {token} test is held in April.</p>", NewsCategory.AdmissionInfo);
        using var guest = _open.CreateClient();

        var answer = await AskAsync(guest, Public, $"What about {token}?");

        Assert.True(answer.Known);
        Assert.Contains("held in April", answer.Answer);
    }

    [Fact]
    public async Task A_public_announcement_is_a_source_and_links_to_its_public_page()
    {
        var token = Token();
        using var admin = Editor();
        var announcement = await AnnouncementAsync(admin, $"Open day {token}", $"<p>The {token} open day is on Saturday.</p>", isPublic: true);
        using var guest = _open.CreateClient();

        var answer = await AskAsync(guest, Public, $"When is {token}?");

        Assert.True(answer.Known);
        Assert.Equal($"/announcements/public/{announcement.Id}", Assert.Single(answer.Sources).Url);
    }

    // ---- what is not an answer ----

    [Fact]
    public async Task A_question_the_sources_do_not_cover_is_answered_with_i_do_not_know_and_the_staff_contact_without_asking_the_ai()
    {
        using var guest = _open.CreateClient();
        var before = await ChatbotCallsAsync();

        var answer = await AskAsync(guest, Public, $"What is the capital of {Token()}?");

        Assert.False(answer.Known);
        Assert.Empty(answer.Sources);
        Assert.False(string.IsNullOrWhiteSpace(answer.Contact));
        Assert.Contains(answer.Contact!, answer.Answer, StringComparison.Ordinal);
        Assert.Equal(before, await ChatbotCallsAsync());
    }

    [Fact]
    public async Task A_question_of_filler_words_only_is_not_answered_either()
    {
        using var guest = _open.CreateClient();

        var answer = await AskAsync(guest, Public, "What is it? Can you tell me?");

        Assert.False(answer.Known);
    }

    [Fact]
    public async Task A_draft_article_and_an_unpublished_announcement_are_never_found()
    {
        var token = Token();
        using var admin = Editor();
        await NewsAsync(admin, $"Secret {token}", $"<p>The {token} plan.</p>", publish: false);
        await AnnouncementAsync(admin, $"Secret {token}", $"<p>The {token} plan.</p>", isPublic: true, publish: false);
        using var guest = _open.CreateClient();

        Assert.False((await AskAsync(guest, Public, $"What is the {token} plan?")).Known);
    }

    [Fact]
    public async Task An_announcement_that_is_not_public_is_never_shown_to_a_guest()
    {
        var token = Token();
        using var admin = Editor();
        await AnnouncementAsync(admin, $"Staff {token}", $"<p>The {token} meeting is on Friday.</p>", isPublic: false);
        using var guest = _open.CreateClient();

        var answer = await AskAsync(guest, Public, $"When is the {token} meeting?");

        Assert.False(answer.Known);
        Assert.DoesNotContain("Friday", answer.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_guest_call_is_logged_without_a_user_and_uses_nobodys_daily_calls()
    {
        var token = Token();
        using var admin = Editor();
        await NewsAsync(admin, $"Fees {token}", $"<p>The {token} fee is low.</p>");
        using var guest = _open.CreateClient();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.AiUsageLogs.CountAsync(l => l.Feature == "AI-08" && l.UserId == null);

        await AskAsync(guest, Public, $"What is the {token} fee?");

        Assert.Equal(before + 1, await db.AiUsageLogs.CountAsync(l => l.Feature == "AI-08" && l.UserId == null));
    }

    // ---- the parent widget ----

    [Fact]
    public async Task A_parent_also_gets_answers_from_announcements_meant_for_parents_but_a_guest_does_not()
    {
        var token = Token();
        using var admin = Editor();
        await AnnouncementAsync(admin, $"Parents {token}", $"<p>The {token} meeting is on Friday.</p>", isPublic: false, roles: [UserRole.Parent]);
        using var parent = _open.AsUser($"parent-{token}@test.example", "Pia Parent", $"sub-{token}", "Parent");
        using var guest = _open.CreateClient();

        var asked = await AskAsync(parent, Parent, $"When is the {token} meeting?");

        Assert.True(asked.Known);
        Assert.Contains("Friday", asked.Answer);
        Assert.False((await AskAsync(guest, Public, $"When is the {token} meeting?")).Known);
    }

    [Fact]
    public async Task A_parent_does_not_get_answers_from_announcements_meant_for_others()
    {
        var token = Token();
        using var admin = Editor();
        await AnnouncementAsync(admin, $"Teachers {token}", $"<p>The {token} training is on Monday.</p>", isPublic: false, roles: [UserRole.Teacher]);
        using var parent = _open.AsUser($"parent-{token}@test.example", "Pia Parent", $"sub-{token}", "Parent");

        Assert.False((await AskAsync(parent, Parent, $"When is the {token} training?")).Known);
    }

    [Fact]
    public async Task A_parent_gets_answers_from_the_announcement_of_the_classroom_of_their_child()
    {
        var token = Token();
        var scenario = await ClassroomScenario.CreateAsync(_open, 1);
        // A parent of its own: the demo parent has exactly two children, which other tests count on.
        var email = $"parent-{token}@test.example";
        using var parent = _open.AsUser(email, "Pia Parent", $"sub-{token}", "Parent");
        (await parent.GetAsync("/api/v1/announcements/mine")).EnsureSuccessStatusCode(); // the first request creates the user record
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var parentId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            db.StudentParents.Add(StudentParent.Create(parentId, scenario.Students[0].Id));
            await db.SaveChangesAsync();
        }

        using var admin = Editor();
        await AnnouncementAsync(admin, $"Class {token}", $"<p>The {token} trip is on Thursday.</p>", isPublic: false, roles: [UserRole.Parent], classrooms: [scenario.Classroom.Id]);

        var answer = await AskAsync(parent, Parent, $"When is the {token} trip?");

        Assert.True(answer.Known, answer.Answer);
        Assert.Contains("Thursday", answer.Answer);
    }

    // ---- who may ask, how much ----

    [Fact]
    public async Task Invalid_questions_are_a_validation_error()
    {
        using var guest = _open.CreateClient();

        foreach (var question in new[] { "", "   ", new string('q', 301) })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostJsonAsync(Public, new AskChatbotCommand(question))).StatusCode);
        }
    }

    [Fact]
    public async Task Only_parents_use_the_parent_widget_and_guests_are_turned_away_from_it()
    {
        using var teacher = _open.AsUser("teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");
        using var student = _open.AsUser("student@schomans.demo", "Rina Putri", "sub-demo-student", "Student");
        using var admin = Editor();
        using var guest = _open.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostJsonAsync(Parent, new AskChatbotCommand("Hello there"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostJsonAsync(Parent, new AskChatbotCommand("Hello there"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostJsonAsync(Parent, new AskChatbotCommand("Hello there"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostJsonAsync(Parent, new AskChatbotCommand("Hello there"))).StatusCode);
    }

    [Fact]
    public async Task A_guest_may_ask_only_a_few_questions_a_minute()
    {
        await using var limited = fixture.CreateFactory(new Dictionary<string, string> { ["RateLimiting:ChatbotPerMinute"] = "2" });
        using var guest = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await guest.PostJsonAsync(Public, new AskChatbotCommand("When is the open day?"))).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
    }
}
