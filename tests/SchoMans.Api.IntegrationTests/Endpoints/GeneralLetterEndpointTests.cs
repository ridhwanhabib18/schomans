using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.GeneralLetters;
using SchoMans.Application.Features.Letters;
using SchoMans.Domain.Communication;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// AI-09, the system's part of a general letter: staff issue the text they approved; the system adds the next number of the year, the
/// letterhead and the signature of the signed-in person, and stores the PDF. Nothing is sent to anybody.
/// </summary>
[Collection(ApiCollection.Name)]
public class GeneralLetterEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/general-letters";

    private HttpClient Admin() => fixture.AsUser("letters-admin@test.example", "Tia Admin", "sub-letters-admin", "Admin");

    private static IssueGeneralLetterCommand Command(GeneralLetterKind kind = GeneralLetterKind.Invitation, string recipient = "Parents of class 7A", string subject = "Open day", params string[] paragraphs) =>
        new(kind, recipient, subject, paragraphs.Length == 0 ? ["We invite you to the open day.", "Please come at nine."] : [.. paragraphs]);

    private async Task<GeneralLetterDto> IssueAsync(HttpClient client, IssueGeneralLetterCommand command)
    {
        var response = await client.PostJsonAsync(Url, command);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<GeneralLetterDto>();
    }

    [Fact]
    public async Task Staff_issue_a_letter_with_the_next_number_their_own_signature_and_a_stored_pdf()
    {
        using var admin = Admin();

        var letter = await IssueAsync(admin, Command());

        Assert.Matches(new Regex(@"^\d{3,}/UND/SCHOMANS/[IVX]+/\d{4}$"), letter.LetterNumber);
        Assert.Equal((GeneralLetterKind.Invitation, "Parents of class 7A", "Open day", "Tia Admin"), (letter.Kind, letter.Recipient, letter.Subject, letter.SignedByName));

        var url = await (await admin.GetAsync($"{Url}/{letter.Id}/url")).ReadAsync<LetterUrlDto>();
        Assert.Equal(letter.LetterNumber, url.LetterNumber);
        Assert.StartsWith("https://storage.test/private/general-letters/", url.Url, StringComparison.Ordinal);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().GeneralLetters.AsNoTracking().SingleAsync(l => l.Id == letter.Id);
        var pdf = await fixture.Storage.DownloadAsync(FileBucket.Private, stored.PdfKey, CancellationToken.None);
        Assert.NotNull(pdf);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf![..4]), StringComparison.Ordinal);
        Assert.Equal("We invite you to the open day.\n\nPlease come at nine.", stored.Body);
    }

    [Fact]
    public async Task Letters_of_one_kind_get_different_numbers_in_order_and_a_notice_has_its_own_code()
    {
        using var admin = Admin();

        var first = await IssueAsync(admin, Command());
        var second = await IssueAsync(admin, Command());
        var notice = await IssueAsync(admin, Command(GeneralLetterKind.Notice, "All staff", "Holiday"));

        static int Sequence(string number) => int.Parse(number.Split('/')[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Sequence(first.LetterNumber) + 1, Sequence(second.LetterNumber));
        Assert.Contains("/PEM/", notice.LetterNumber, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Letters_issued_at_the_same_moment_do_not_share_a_number()
    {
        using var admin = Admin();

        var letters = await Task.WhenAll(IssueAsync(admin, Command()), IssueAsync(admin, Command()));

        Assert.NotEqual(letters[0].LetterNumber, letters[1].LetterNumber);
    }

    [Fact]
    public async Task The_list_shows_issued_letters_newest_first_with_paging()
    {
        using var admin = Admin();
        var older = await IssueAsync(admin, Command(subject: "Older"));
        var newer = await IssueAsync(admin, Command(subject: "Newer"));

        var page = await (await admin.GetAsync($"{Url}?page=1&pageSize=100")).ReadAsync<PagedResult<GeneralLetterDto>>();

        var ids = page.Items.Select(l => l.Id).ToList();
        Assert.True(ids.IndexOf(newer.Id) < ids.IndexOf(older.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"{Url}?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task An_invalid_letter_is_a_validation_error()
    {
        using var admin = Admin();
        var long800 = new string('p', 800);

        IssueGeneralLetterCommand[] cases =
        [
            Command(recipient: ""),
            Command(recipient: new string('r', GeneralLetter.RecipientMaxLength + 1)),
            Command(subject: " "),
            Command(subject: new string('s', GeneralLetter.SubjectMaxLength + 1)),
            new(GeneralLetterKind.Notice, "All", "Subject", []),
            new(GeneralLetterKind.Notice, "All", "Subject", [" "]),
            new(GeneralLetterKind.Notice, "All", "Subject", [new string('p', 801)]),
            new(GeneralLetterKind.Notice, "All", "Subject", [.. Enumerable.Repeat("One.", 9)]),
            new(GeneralLetterKind.Notice, "All", "Subject", [.. Enumerable.Repeat(long800, 6)]),
            new((GeneralLetterKind)99, "All", "Subject", ["One."]),
        ];

        foreach (var command in cases)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync(Url, command)).StatusCode);
        }
    }

    [Fact]
    public async Task The_principal_may_issue_and_read_letters()
    {
        using var principal = fixture.AsPrincipal();

        var letter = await IssueAsync(principal, Command());

        Assert.Equal("Hendra Wirawan", letter.SignedByName);
        Assert.Equal(HttpStatusCode.OK, (await principal.GetAsync($"{Url}/{letter.Id}/url")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_letter_has_no_download_address()
    {
        using var admin = Admin();

        var response = await admin.GetAsync($"{Url}/{Guid.NewGuid()}/url");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("general_letters.not_found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Teachers_parents_and_students_may_not_use_general_letters_and_guests_are_turned_away()
    {
        using var admin = Admin();
        var letter = await IssueAsync(admin, Command());
        using var teacher = fixture.AsTeacher();
        using var parent = fixture.AsParent();
        using var student = fixture.AsStudent();
        using var guest = fixture.Factory.CreateClient();

        foreach (var client in new[] { teacher, parent, student })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, Command())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/{letter.Id}/url")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(Url)).StatusCode);
    }
}
