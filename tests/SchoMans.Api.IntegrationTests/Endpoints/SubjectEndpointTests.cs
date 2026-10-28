using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Subjects;
using SchoMans.Application.Features.Subjects.CreateSubject;
using SchoMans.Application.Features.Subjects.UpdateSubject;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class SubjectEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/subjects";

    private static string UniqueName() => $"Subject {Guid.NewGuid():N}"[..24];

    [Fact]
    public async Task The_seeded_subjects_are_listed_for_admin_principal_and_teacher()
    {
        foreach (var client in new[] { fixture.AsAdmin(), fixture.AsPrincipal(), fixture.AsTeacher() })
        {
            using (client)
            {
                var page = await (await client.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<SubjectDto>>();
                Assert.Contains(page.Items, s => s.Name == "Mathematics" && s.MinimumPassingScore == 75);
            }
        }
    }

    [Fact]
    public async Task Subjects_can_be_searched_and_sorted_by_passing_score()
    {
        using var admin = fixture.AsAdmin();

        var search = await (await admin.GetAsync($"{Url}?search=Math")).ReadAsync<PagedResult<SubjectDto>>();
        var sorted = await (await admin.GetAsync($"{Url}?sort=minimumPassingScore&pageSize=100")).ReadAsync<PagedResult<SubjectDto>>();

        Assert.All(search.Items, s => Assert.Contains("Math", s.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(sorted.Items.Select(s => s.MinimumPassingScore).Order(), sorted.Items.Select(s => s.MinimumPassingScore));
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    public async Task Other_roles_may_not_see_subjects(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Admin_creates_updates_and_deletes_a_subject()
    {
        using var admin = fixture.AsAdmin();
        var name = UniqueName();

        var created = await admin.PostJsonAsync(Url, new CreateSubjectCommand(name, 72.5m));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var subject = await created.ReadAsync<SubjectDto>();
        Assert.Equal(72.5m, subject.MinimumPassingScore);

        var updated = await admin.PutJsonAsync($"{Url}/{subject.Id}", new UpdateSubjectCommand(subject.Id, name + " II", 80, subject.RowVersion));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(80, (await updated.ReadAsync<SubjectDto>()).MinimumPassingScore);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Url}/{subject.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{subject.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_subject_name_must_be_unique()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateSubjectCommand("Mathematics", 70));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("subjects.name_taken", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.5)]
    public async Task The_minimum_passing_score_must_be_between_0_and_100(double score)
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateSubjectCommand(UniqueName(), (decimal)score));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_stale_subject_update_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var subject = await (await admin.PostJsonAsync(Url, new CreateSubjectCommand(UniqueName(), 70))).ReadAsync<SubjectDto>();
        var update = new UpdateSubjectCommand(subject.Id, subject.Name, 71, subject.RowVersion);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutJsonAsync($"{Url}/{subject.Id}", update)).StatusCode);
        var stale = await admin.PutJsonAsync($"{Url}/{subject.Id}", update with { MinimumPassingScore = 90 });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    public async Task Only_admin_may_change_subjects(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, new CreateSubjectCommand(UniqueName(), 70))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }
}
