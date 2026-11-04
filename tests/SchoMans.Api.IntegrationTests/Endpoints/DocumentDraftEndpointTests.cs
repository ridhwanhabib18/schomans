using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Drafts;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// The test set of AI-09 (announcement and letter drafts): staff give the kind of document, the audience and notes, and the AI drafts a
/// title and paragraphs for the editor. Nothing is saved, published or sent. The AI provider is the deterministic mock.
/// </summary>
[Collection(ApiCollection.Name)]
public class DocumentDraftEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/drafts";

    // A fresh signed-in editor each time keeps the daily AI limit of the shared demo accounts untouched.
    private HttpClient Editor(string role = "Admin") => fixture.AsNewUser(role);

    private static DraftDocumentCommand Command(DraftDocumentType type = DraftDocumentType.Announcement, string audience = "Parents of class 7A", string notes = "Open day on Saturday. Bring the report card.") =>
        new(type, audience, notes);

    [Theory]
    [InlineData(DraftDocumentType.Announcement)]
    [InlineData(DraftDocumentType.Invitation)]
    [InlineData(DraftDocumentType.Notice)]
    public async Task Staff_get_a_title_and_paragraphs_built_from_their_notes(DraftDocumentType type)
    {
        using var admin = Editor();

        var response = await admin.PostJsonAsync(Url, Command(type));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await response.ReadAsync<DocumentDraftDto>();
        Assert.Contains("Open day on Saturday", draft.Title);
        Assert.InRange(draft.Paragraphs.Count, 1, 8);
        Assert.Contains(draft.Paragraphs, p => p.Contains("Bring the report card", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_principal_may_draft_too()
    {
        using var principal = Editor("Principal");

        Assert.Equal(HttpStatusCode.OK, (await principal.PostJsonAsync(Url, Command())).StatusCode);
    }

    [Fact]
    public async Task A_draft_is_not_stored_anywhere_and_uses_one_daily_ai_call_of_the_person()
    {
        using var admin = fixture.AsUser("drafter@test.example", "Dina Drafter", "sub-drafter", "Admin");
        (await admin.PostJsonAsync(Url, Command())).EnsureSuccessStatusCode(); // the first request also creates the user record
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == "drafter@test.example").Select(u => u.Id).SingleAsync();
        var announcements = await db.Announcements.CountAsync();
        var letters = await db.GeneralLetters.CountAsync();
        var calls = await db.AiUsageLogs.CountAsync(l => l.UserId == userId && l.Feature == "AI-09" && !l.IsBulk);

        (await admin.PostJsonAsync(Url, Command())).EnsureSuccessStatusCode();

        Assert.Equal((announcements, letters), (await db.Announcements.CountAsync(), await db.GeneralLetters.CountAsync()));
        Assert.Equal(calls + 1, await db.AiUsageLogs.CountAsync(l => l.UserId == userId && l.Feature == "AI-09" && !l.IsBulk));
    }

    [Fact]
    public async Task Invalid_input_is_a_validation_error()
    {
        using var admin = Editor();
        var tooLong = new string('n', DraftDocumentValidator.NotesMaxLength + 1);

        DraftDocumentCommand[] cases =
        [
            Command(audience: ""),
            Command(audience: new string('a', DraftDocumentValidator.AudienceMaxLength + 1)),
            Command(notes: " "),
            Command(notes: tooLong),
            Command((DraftDocumentType)99),
        ];

        foreach (var command in cases)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(Url, command)).StatusCode);
        }
    }

    [Fact]
    public async Task Teachers_parents_and_students_may_not_draft_and_guests_are_turned_away()
    {
        using var teacher = fixture.AsTeacher();
        using var parent = fixture.AsParent();
        using var student = fixture.AsStudent();
        using var guest = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostJsonAsync(Url, Command())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostJsonAsync(Url, Command())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostJsonAsync(Url, Command())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostJsonAsync(Url, Command())).StatusCode);
    }
}
