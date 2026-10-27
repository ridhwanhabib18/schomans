using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.AcademicYears;
using SchoMans.Application.Features.Classrooms;
using SchoMans.Application.Features.Classrooms.CreateClassroom;
using SchoMans.Application.Features.Classrooms.UpdateClassroom;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class ClassroomEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/classrooms";

    private async Task<AcademicYearDto> ActiveYearAsync(HttpClient admin) =>
        (await (await admin.GetAsync("/api/v1/academic-years")).ReadAsync<PagedResult<AcademicYearDto>>()).Items.Single(y => y.IsActive);

    private static string UniqueName() => $"Test {Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task Admin_principal_and_teacher_can_list_classrooms_with_their_student_counts()
    {
        foreach (var client in new[] { fixture.AsAdmin(), fixture.AsPrincipal(), fixture.AsTeacher() })
        {
            using (client)
            {
                var page = await (await client.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<ClassroomDto>>();
                Assert.Contains(page.Items, c => c.Name == "Class A" && c.StudentCount >= 30 && c.HomeroomTeacherName == "Budi Santoso");
            }
        }
    }

    [Fact]
    public async Task Classrooms_can_be_filtered_by_academic_year_and_searched()
    {
        using var admin = fixture.AsAdmin();
        var year = await ActiveYearAsync(admin);

        var inYear = await (await admin.GetAsync($"{Url}?academicYearId={year.Id}&pageSize=100")).ReadAsync<PagedResult<ClassroomDto>>();
        var searched = await (await admin.GetAsync($"{Url}?search=Class%20B&academicYearId={year.Id}")).ReadAsync<PagedResult<ClassroomDto>>();
        var otherYear = await (await admin.GetAsync($"{Url}?academicYearId={Guid.NewGuid()}")).ReadAsync<PagedResult<ClassroomDto>>();

        Assert.All(inYear.Items, c => Assert.Equal(year.Id, c.AcademicYearId));
        Assert.Contains(searched.Items, c => c.Name == "Class B");
        Assert.Equal(0, otherYear.TotalCount);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Applicant")]
    public async Task Other_roles_may_not_see_classrooms(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Admin_creates_a_classroom_and_it_starts_without_students()
    {
        using var admin = fixture.AsAdmin();
        var year = await ActiveYearAsync(admin);
        var teacher = await TestData.CreateTeacherAsync(admin);

        var response = await admin.PostJsonAsync(Url, new CreateClassroomCommand(UniqueName(), year.Id, teacher.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var classroom = await response.ReadAsync<ClassroomDto>();
        Assert.Equal(0, classroom.StudentCount);
        Assert.Equal(teacher.Id, classroom.HomeroomTeacherId);
        Assert.Equal(year.Name, classroom.AcademicYearName);
    }

    [Fact]
    public async Task A_classroom_name_is_unique_within_an_academic_year()
    {
        using var admin = fixture.AsAdmin();
        var year = await ActiveYearAsync(admin);
        var teacher = await TestData.CreateTeacherAsync(admin);

        var response = await admin.PostJsonAsync(Url, new CreateClassroomCommand("class a", year.Id, teacher.Id)); // "Class A" exists

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("classrooms.name_taken", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_classroom_needs_an_existing_year_and_an_existing_homeroom_teacher()
    {
        using var admin = fixture.AsAdmin();
        var year = await ActiveYearAsync(admin);
        var teacher = await TestData.CreateTeacherAsync(admin);

        var noYear = await admin.PostJsonAsync(Url, new CreateClassroomCommand(UniqueName(), Guid.NewGuid(), teacher.Id));
        var noTeacher = await admin.PostJsonAsync(Url, new CreateClassroomCommand(UniqueName(), year.Id, Guid.NewGuid()));
        var invalid = await admin.PostJsonAsync(Url, new CreateClassroomCommand("", Guid.Empty, Guid.Empty));

        Assert.Equal("academic_years.not_found", await noYear.ProblemCodeAsync());
        Assert.Equal("teachers.not_found", await noTeacher.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    public async Task Only_admin_may_create_classrooms(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.PostJsonAsync(Url, new CreateClassroomCommand("X", Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_changes_the_homeroom_teacher_and_a_stale_update_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var year = await ActiveYearAsync(admin);
        var first = await TestData.CreateTeacherAsync(admin);
        var second = await TestData.CreateTeacherAsync(admin);
        var classroom = await (await admin.PostJsonAsync(Url, new CreateClassroomCommand(UniqueName(), year.Id, first.Id))).ReadAsync<ClassroomDto>();
        var update = new UpdateClassroomCommand(classroom.Id, classroom.Name, second.Id, classroom.RowVersion);

        var changed = await admin.PutJsonAsync($"{Url}/{classroom.Id}", update);
        var stale = await admin.PutJsonAsync($"{Url}/{classroom.Id}", update with { Name = UniqueName() });

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(second.Id, (await changed.ReadAsync<ClassroomDto>()).HomeroomTeacherId);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());
    }

    [Fact]
    public async Task Renaming_to_the_name_of_another_classroom_of_the_year_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var classE = await TestData.ClassroomAsync(admin, "Class E");

        var response = await admin.PutJsonAsync($"{Url}/{classE.Id}", new UpdateClassroomCommand(classE.Id, "Class A", classE.HomeroomTeacherId, classE.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("classrooms.name_taken", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_classroom_with_students_cannot_be_deleted_but_an_empty_one_can()
    {
        using var admin = fixture.AsAdmin();
        var year = await ActiveYearAsync(admin);
        var teacher = await TestData.CreateTeacherAsync(admin);
        var classA = await TestData.ClassroomAsync(admin);
        var empty = await (await admin.PostJsonAsync(Url, new CreateClassroomCommand(UniqueName(), year.Id, teacher.Id))).ReadAsync<ClassroomDto>();

        var withStudents = await admin.DeleteAsync($"{Url}/{classA.Id}");
        var removed = await admin.DeleteAsync($"{Url}/{empty.Id}");

        Assert.Equal(HttpStatusCode.Conflict, withStudents.StatusCode);
        Assert.Equal("classrooms.has_students", await withStudents.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{empty.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_classroom_that_does_not_exist_is_not_found()
    {
        using var admin = fixture.AsAdmin();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }
}
