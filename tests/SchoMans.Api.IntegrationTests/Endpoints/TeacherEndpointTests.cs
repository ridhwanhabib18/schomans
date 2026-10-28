using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Teachers;
using SchoMans.Application.Features.Teachers.CreateTeacher;
using SchoMans.Application.Features.Teachers.UpdateTeacher;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class TeacherEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/teachers";

    [Fact]
    public async Task Admin_and_principal_list_teachers()
    {
        using var admin = fixture.AsAdmin();
        using var principal = fixture.AsPrincipal();

        var asAdmin = await (await admin.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<TeacherDto>>();
        var asPrincipal = await principal.GetAsync($"{Url}?pageSize=100");

        Assert.True(asAdmin.TotalCount >= 15);
        Assert.Equal(HttpStatusCode.OK, asPrincipal.StatusCode);
        Assert.Contains(asAdmin.Items, t => t.Email == "teacher@schomans.demo" && t.IsHomeroomTeacher);
    }

    [Fact]
    public async Task Search_and_sort_work_on_the_teacher_list()
    {
        using var admin = fixture.AsAdmin();
        var teacher = await TestData.CreateTeacherAsync(admin);

        var byNip = await (await admin.GetAsync($"{Url}?search={teacher.Nip}")).ReadAsync<PagedResult<TeacherDto>>();
        var sorted = await (await admin.GetAsync($"{Url}?sort=nip&desc=true&pageSize=100")).ReadAsync<PagedResult<TeacherDto>>();

        Assert.Single(byNip.Items);
        Assert.Equal(sorted.Items.Select(t => t.Nip).OrderByDescending(n => n, StringComparer.Ordinal), sorted.Items.Select(t => t.Nip));
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Student")]
    [InlineData("Parent")]
    public async Task Other_roles_may_not_see_the_teacher_list(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Admin_creates_a_teacher()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateTeacherCommand("New Teacher", TestData.NextEmail("t"), TestData.NextNip()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var teacher = await response.ReadAsync<TeacherDto>();
        Assert.False(teacher.IsHomeroomTeacher);
        Assert.False(teacher.HasAccount);
        Assert.Equal($"{Url}/{teacher.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task A_duplicate_nip_or_email_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var existing = await TestData.CreateTeacherAsync(admin);

        var sameNip = await admin.PostJsonAsync(Url, new CreateTeacherCommand("Other", TestData.NextEmail(), existing.Nip));
        var sameEmail = await admin.PostJsonAsync(Url, new CreateTeacherCommand("Other", existing.Email, TestData.NextNip()));

        Assert.Equal(HttpStatusCode.Conflict, sameNip.StatusCode);
        Assert.Equal("teachers.nip_taken", await sameNip.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, sameEmail.StatusCode);
        Assert.Equal("users.email_taken", await sameEmail.ProblemCodeAsync());
    }

    [Fact]
    public async Task An_invalid_teacher_is_rejected()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateTeacherCommand("", "nope", "123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nip", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    public async Task Only_admin_may_create_teachers(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.PostJsonAsync(Url, new CreateTeacherCommand("New", TestData.NextEmail(), TestData.NextNip()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_updates_a_teacher_and_a_stale_update_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var teacher = await TestData.CreateTeacherAsync(admin);
        var update = new UpdateTeacherCommand(teacher.Id, "Renamed Teacher", teacher.Email, teacher.Nip, teacher.RowVersion);

        var first = await admin.PutJsonAsync($"{Url}/{teacher.Id}", update);
        var stale = await admin.PutJsonAsync($"{Url}/{teacher.Id}", update with { FullName = "Late Editor" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("Renamed Teacher", (await first.ReadAsync<TeacherDto>()).FullName);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_teacher_that_does_not_exist_is_not_found()
    {
        using var admin = fixture.AsAdmin();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task A_homeroom_teacher_cannot_be_deleted_until_somebody_else_takes_the_classroom()
    {
        using var admin = fixture.AsAdmin();
        var homeroom = (await (await admin.GetAsync($"{Url}?search=teacher@schomans.demo")).ReadAsync<PagedResult<TeacherDto>>()).Items.Single();

        var response = await admin.DeleteAsync($"{Url}/{homeroom.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("teachers.is_homeroom", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_teacher_without_a_classroom_can_be_deleted()
    {
        using var admin = fixture.AsAdmin();
        var teacher = await TestData.CreateTeacherAsync(admin);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Url}/{teacher.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{teacher.Id}")).StatusCode);
    }
}
