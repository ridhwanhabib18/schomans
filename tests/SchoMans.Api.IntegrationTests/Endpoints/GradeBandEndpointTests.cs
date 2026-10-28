using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.GradeBands;
using SchoMans.Application.Features.GradeBands.ReplaceGradeBands;
using SchoMans.Application.Features.Grading.GetGradeBook;
using SchoMans.Domain.Grading;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>The band scale is global, so these tests get a database of their own instead of the shared one.</summary>
public class GradeBandEndpointTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Url = "/api/v1/grade-bands";

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Teacher")]
    [InlineData("Principal")]
    [InlineData("Admin")]
    public async Task Every_role_reads_the_default_scale_highest_first(string role)
    {
        using var client = fixture.AsNewUser(role);

        var bands = await (await client.GetAsync(Url)).ReadAsync<List<GradeBandDto>>();

        Assert.Equal(["A", "B", "C", "D", "E"], bands.Select(b => b.Label));
        Assert.Equal([90m, 80m, 70m, 60m, 0m], bands.Select(b => b.MinScore));
    }

    [Fact]
    public async Task The_admin_replaces_the_scale_and_grades_get_the_new_labels()
    {
        using var admin = fixture.AsAdmin();
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Final, 86);

        var replaced = await admin.PutJsonAsync(Url, new ReplaceGradeBandsCommand([new("Excellent", 85), new("Good", 65), new("Needs work", 0)]));
        var bands = await replaced.ReadAsync<List<GradeBandDto>>();
        var row = Assert.Single((await (await teacher.GetAsync($"/api/v1/grades/book?classroomId={scenario.Classroom.Id}&subjectId={scenario.Subject.Id}&termId={termId}")).ReadAsync<GradeBookDto>()).Rows);

        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal(["Excellent", "Good", "Needs work"], bands.Select(b => b.Label));
        Assert.Equal("Excellent", row.Band);

        // Back to the default, so the order of the tests in this class does not matter.
        (await admin.PutJsonAsync(Url, new ReplaceGradeBandsCommand([new("A", 90), new("B", 80), new("C", 70), new("D", 60), new("E", 0)]))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_scale_without_a_band_at_zero_or_with_duplicates_is_a_validation_error()
    {
        using var admin = fixture.AsAdmin();

        var noZero = await admin.PutJsonAsync(Url, new ReplaceGradeBandsCommand([new("A", 90), new("B", 50)]));
        var duplicate = await admin.PutJsonAsync(Url, new ReplaceGradeBandsCommand([new("A", 90), new("A", 0)]));

        Assert.Equal(HttpStatusCode.BadRequest, noZero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Principal")]
    [InlineData("Student")]
    [InlineData("Parent")]
    public async Task Only_the_admin_changes_the_scale(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.PutJsonAsync(Url, new ReplaceGradeBandsCommand([new("A", 0)]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
