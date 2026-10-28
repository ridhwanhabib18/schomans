using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.AcademicYears;
using SchoMans.Application.Features.AcademicYears.CreateAcademicYear;
using SchoMans.Application.Features.AcademicYears.UpdateAcademicYear;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class AcademicYearEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/academic-years";

    private static readonly DateOnly Start = new(2090, 7, 13);
    private static readonly DateOnly End = new(2091, 6, 19);

    private static CreateAcademicYearCommand NewYear(string? name = null) => new(
        name ?? $"2090/{Guid.NewGuid():N}"[..20],
        Start,
        End,
        [new TermInput(null, "Term 1", Start, new DateOnly(2090, 12, 20)), new TermInput(null, "Term 2", new DateOnly(2091, 1, 5), End)]);

    private async Task<List<AcademicYearDto>> ListAsync(HttpClient client) =>
        (await (await client.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<AcademicYearDto>>()).Items.ToList();

    [Fact]
    public async Task Staff_lists_academic_years_newest_first_with_their_terms()
    {
        foreach (var client in new[] { fixture.AsAdmin(), fixture.AsPrincipal(), fixture.AsTeacher() })
        {
            using (client)
            {
                var years = await ListAsync(client);
                var current = years.Single(y => y.IsActive);
                Assert.Equal("2026/2027", current.Name);
                Assert.Equal(["Term 1", "Term 2"], current.Terms.Select(t => t.Name));
                Assert.True(current.ClassroomCount >= 5);
                Assert.Equal(years.OrderByDescending(y => y.StartDate).Select(y => y.Id), years.Select(y => y.Id));
            }
        }
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Applicant")]
    public async Task Other_roles_may_not_see_academic_years(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task One_academic_year_can_be_read_and_an_unknown_one_is_not_found()
    {
        using var admin = fixture.AsAdmin();
        var current = (await ListAsync(admin)).Single(y => y.IsActive);

        var found = await admin.GetAsync($"{Url}/{current.Id}");
        var missing = await admin.GetAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(current.Name, (await found.ReadAsync<AcademicYearDto>()).Name);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task A_new_academic_year_starts_inactive_with_its_first_term_current()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, NewYear());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var year = await response.ReadAsync<AcademicYearDto>();
        Assert.False(year.IsActive);
        Assert.Equal(2, year.Terms.Count);
        Assert.True(year.Terms[0].IsActive);
        Assert.False(year.Terms[1].IsActive);
    }

    [Fact]
    public async Task An_academic_year_name_must_be_unique()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, NewYear("2026/2027"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("academic_years.name_taken", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Terms_must_lie_inside_the_year_and_have_different_names()
    {
        using var admin = fixture.AsAdmin();
        var outside = NewYear() with { Terms = [new TermInput(null, "Term 1", Start.AddDays(-5), new DateOnly(2090, 12, 20))] };
        var sameName = NewYear() with { Terms = [new TermInput(null, "Term 1", Start, new DateOnly(2090, 12, 20)), new TermInput(null, "term 1", new DateOnly(2091, 1, 5), End)] };

        var first = await admin.PostJsonAsync(Url, outside);
        var second = await admin.PostJsonAsync(Url, sameName);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal("academic_years.invalid", await first.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task An_invalid_year_is_rejected_by_validation()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateAcademicYearCommand("", End, Start, []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    public async Task Only_admin_may_change_academic_years(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, NewYear())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Url}/{Guid.NewGuid()}/activate", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_renames_a_year_edits_a_term_and_adds_a_term()
    {
        using var admin = fixture.AsAdmin();
        var year = await (await admin.PostJsonAsync(Url, NewYear())).ReadAsync<AcademicYearDto>();
        var renamed = $"{year.Name}-r";
        var update = new UpdateAcademicYearCommand(
            year.Id,
            renamed,
            year.StartDate,
            year.EndDate,
            [
                new TermInput(year.Terms[0].Id, "First term", year.Terms[0].StartDate, year.Terms[0].EndDate),
                new TermInput(null, "Holiday course", new DateOnly(2091, 2, 1), new DateOnly(2091, 2, 20)),
            ],
            year.RowVersion);

        var response = await admin.PutJsonAsync($"{Url}/{year.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<AcademicYearDto>();
        Assert.Equal(renamed, updated.Name);
        // The terms that were left out of the request are kept: a term is never removed here.
        Assert.Equal(["First term", "Term 2", "Holiday course"], updated.Terms.Select(t => t.Name));
    }

    [Fact]
    public async Task A_term_of_another_year_cannot_be_edited_through_this_one()
    {
        using var admin = fixture.AsAdmin();
        var year = await (await admin.PostJsonAsync(Url, NewYear())).ReadAsync<AcademicYearDto>();
        var current = (await ListAsync(admin)).Single(y => y.IsActive);
        var update = new UpdateAcademicYearCommand(year.Id, year.Name, year.StartDate, year.EndDate, [new TermInput(current.Terms[0].Id, "Hijack", Start, End)], year.RowVersion);

        var response = await admin.PutJsonAsync($"{Url}/{year.Id}", update);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("terms.not_found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_stale_academic_year_update_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var year = await (await admin.PostJsonAsync(Url, NewYear())).ReadAsync<AcademicYearDto>();
        var update = new UpdateAcademicYearCommand(year.Id, $"{year.Name}-1", year.StartDate, year.EndDate, [], year.RowVersion);
        update = update with { Terms = [new TermInput(year.Terms[0].Id, year.Terms[0].Name, year.Terms[0].StartDate, year.Terms[0].EndDate)] };

        Assert.Equal(HttpStatusCode.OK, (await admin.PutJsonAsync($"{Url}/{year.Id}", update)).StatusCode);
        var stale = await admin.PutJsonAsync($"{Url}/{year.Id}", update with { Name = $"{year.Name}-2" });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());
    }

    [Fact]
    public async Task Activating_a_year_makes_it_the_only_current_one()
    {
        using var admin = fixture.AsAdmin();
        var original = (await ListAsync(admin)).Single(y => y.IsActive);
        var other = await (await admin.PostJsonAsync(Url, NewYear())).ReadAsync<AcademicYearDto>();

        try
        {
            var response = await admin.PostAsync($"{Url}/{other.Id}/activate", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var after = await ListAsync(admin);
            Assert.Equal([other.Id], after.Where(y => y.IsActive).Select(y => y.Id));
        }
        finally
        {
            // The database is shared with other tests: put the school back in its original year.
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"{Url}/{original.Id}/activate", null)).StatusCode);
        }

        Assert.Equal([original.Id], (await ListAsync(admin)).Where(y => y.IsActive).Select(y => y.Id));
    }

    [Fact]
    public async Task Activating_the_current_year_again_changes_nothing()
    {
        using var admin = fixture.AsAdmin();
        var current = (await ListAsync(admin)).Single(y => y.IsActive);

        var response = await admin.PostAsync($"{Url}/{current.Id}/activate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadAsync<AcademicYearDto>()).IsActive);
    }

    [Fact]
    public async Task Activating_an_unknown_year_is_not_found()
    {
        using var admin = fixture.AsAdmin();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"{Url}/{Guid.NewGuid()}/activate", null)).StatusCode);
    }
}
