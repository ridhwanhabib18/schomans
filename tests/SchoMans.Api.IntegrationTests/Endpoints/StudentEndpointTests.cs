using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Students;
using SchoMans.Application.Features.Students.CreateStudent;
using SchoMans.Application.Features.Students.UpdateStudent;
using SchoMans.Application.Features.Users.GetMe;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class StudentEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/students";

    // ---- listing: paging, search, filter, sort ----

    [Fact]
    public async Task Admin_lists_students_page_by_page()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.GetAsync($"{Url}?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.ReadAsync<PagedResult<StudentDto>>();
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.True(page.TotalCount >= 150);
    }

    [Fact]
    public async Task The_next_page_continues_where_the_first_stopped_without_repeats()
    {
        using var admin = fixture.AsAdmin();

        var first = await (await admin.GetAsync($"{Url}?page=1&pageSize=20")).ReadAsync<PagedResult<StudentDto>>();
        var second = await (await admin.GetAsync($"{Url}?page=2&pageSize=20")).ReadAsync<PagedResult<StudentDto>>();

        Assert.Empty(first.Items.Select(s => s.Id).Intersect(second.Items.Select(s => s.Id)));
    }

    [Fact]
    public async Task Students_can_be_filtered_by_classroom()
    {
        using var admin = fixture.AsAdmin();
        var classroom = await TestData.ClassroomAsync(admin, "Class B");

        var page = await (await admin.GetAsync($"{Url}?classroomId={classroom.Id}&pageSize=100")).ReadAsync<PagedResult<StudentDto>>();

        Assert.True(page.TotalCount >= 30);
        Assert.All(page.Items, s => Assert.Equal("Class B", s.ClassroomName));
    }

    [Fact]
    public async Task Search_matches_name_nisn_and_email()
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);

        foreach (var term in new[] { student.Nisn, student.Email, student.Email[..20] })
        {
            var page = await (await admin.GetAsync($"{Url}?search={Uri.EscapeDataString(term)}")).ReadAsync<PagedResult<StudentDto>>();
            Assert.Contains(page.Items, s => s.Id == student.Id);
        }
    }

    [Fact]
    public async Task Search_treats_percent_and_underscore_as_plain_characters()
    {
        using var admin = fixture.AsAdmin();

        var page = await (await admin.GetAsync($"{Url}?search=%25_%25")).ReadAsync<PagedResult<StudentDto>>();

        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Students_can_be_sorted_in_both_directions()
    {
        using var admin = fixture.AsAdmin();

        var ascending = await (await admin.GetAsync($"{Url}?sort=nisn&pageSize=100")).ReadAsync<PagedResult<StudentDto>>();
        var descending = await (await admin.GetAsync($"{Url}?sort=nisn&desc=true&pageSize=100")).ReadAsync<PagedResult<StudentDto>>();

        Assert.Equal(ascending.Items.Select(s => s.Nisn).OrderBy(n => n, StringComparer.Ordinal), ascending.Items.Select(s => s.Nisn));
        Assert.Equal(descending.Items.Select(s => s.Nisn).OrderByDescending(n => n, StringComparer.Ordinal), descending.Items.Select(s => s.Nisn));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("sort=password")]
    public async Task Invalid_list_parameters_are_rejected(string query)
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.GetAsync($"{Url}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- who may read ----

    [Fact]
    public async Task Principal_reads_all_students()
    {
        using var principal = fixture.AsPrincipal();

        var page = await (await principal.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<StudentDto>>();

        Assert.True(page.TotalCount >= 150);
    }

    [Fact]
    public async Task A_teacher_only_sees_the_students_of_the_classroom_they_are_homeroom_teacher_of()
    {
        using var teacher = fixture.AsTeacher();

        var page = await (await teacher.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResult<StudentDto>>();

        Assert.True(page.TotalCount >= 30);
        Assert.All(page.Items, s => Assert.Equal("Class A", s.ClassroomName));
    }

    [Fact]
    public async Task A_teacher_asking_for_a_student_of_another_classroom_gets_not_found_not_forbidden()
    {
        using var admin = fixture.AsAdmin();
        using var teacher = fixture.AsTeacher();
        var otherClass = await TestData.ClassroomAsync(admin, "Class C");
        var student = await TestData.CreateStudentAsync(admin, otherClass.Id);

        var response = await teacher.GetAsync($"{Url}/{student.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Applicant")]
    public async Task Other_roles_may_not_read_students(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Request_without_a_token_is_unauthorized()
    {
        using var client = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Get_returns_one_student_or_not_found()
    {
        using var admin = fixture.AsAdmin();
        var created = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);

        var found = await admin.GetAsync($"{Url}/{created.Id}");
        var missing = await admin.GetAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(created.Nisn, (await found.ReadAsync<StudentDto>()).Nisn);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // ---- create ----

    [Fact]
    public async Task Admin_creates_a_student_who_has_no_account_yet()
    {
        using var admin = fixture.AsAdmin();
        var classroom = await TestData.ClassroomAsync(admin);
        var email = TestData.NextEmail("new");

        var response = await admin.PostJsonAsync(Url, new CreateStudentCommand("New Student", email.ToUpperInvariant(), TestData.NextNisn(), classroom.Id, new DateOnly(2013, 1, 2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var student = await response.ReadAsync<StudentDto>();
        Assert.Equal($"{Url}/{student.Id}", response.Headers.Location?.ToString());
        Assert.Equal(email, student.Email); // stored in lower case
        Assert.Equal("Class A", student.ClassroomName);
        Assert.Equal("Active", student.Status);
        Assert.False(student.HasAccount);
    }

    [Fact]
    public async Task A_duplicate_nisn_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var classroom = await TestData.ClassroomAsync(admin);
        var existing = await TestData.CreateStudentAsync(admin, classroom.Id);

        var response = await admin.PostJsonAsync(Url, new CreateStudentCommand("Other", TestData.NextEmail(), existing.Nisn, classroom.Id, new DateOnly(2013, 1, 2)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("students.nisn_taken", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_duplicate_email_is_rejected_even_when_the_case_differs()
    {
        using var admin = fixture.AsAdmin();
        var classroom = await TestData.ClassroomAsync(admin);
        var existing = await TestData.CreateStudentAsync(admin, classroom.Id);

        var response = await admin.PostJsonAsync(Url, new CreateStudentCommand("Other", existing.Email.ToUpperInvariant(), TestData.NextNisn(), classroom.Id, new DateOnly(2013, 1, 2)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("users.email_taken", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task An_invalid_student_is_rejected_with_the_failing_fields()
    {
        using var admin = fixture.AsAdmin();
        var classroom = await TestData.ClassroomAsync(admin);

        var response = await admin.PostJsonAsync(Url, new CreateStudentCommand("", "not-an-email", "123", classroom.Id, new DateOnly(2013, 1, 2)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("nisn", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fullName", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_student_cannot_be_created_in_a_classroom_that_does_not_exist()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateStudentCommand("New", TestData.NextEmail(), TestData.NextNisn(), Guid.NewGuid(), new DateOnly(2013, 1, 2)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("classrooms.not_found", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    [InlineData("Student")]
    public async Task Only_admin_may_create_students(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.PostJsonAsync(Url, new CreateStudentCommand("New", TestData.NextEmail(), TestData.NextNisn(), Guid.NewGuid(), new DateOnly(2013, 1, 2)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- update ----

    private static UpdateStudentCommand Update(StudentDto student, string? name = null, string? nisn = null, Guid? classroomId = null, string? rowVersion = null) =>
        new(student.Id, name ?? student.FullName, student.Email, nisn ?? student.Nisn, classroomId ?? student.ClassroomId, student.DateOfBirth, rowVersion ?? student.RowVersion);

    [Fact]
    public async Task Admin_updates_a_student_and_moves_them_to_another_classroom()
    {
        using var admin = fixture.AsAdmin();
        var classA = await TestData.ClassroomAsync(admin, "Class A");
        var classD = await TestData.ClassroomAsync(admin, "Class D");
        var student = await TestData.CreateStudentAsync(admin, classA.Id);

        var response = await admin.PutJsonAsync($"{Url}/{student.Id}", Update(student, name: "Renamed Student", classroomId: classD.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<StudentDto>();
        Assert.Equal("Renamed Student", updated.FullName);
        Assert.Equal("Class D", updated.ClassroomName);
        Assert.NotEqual(student.RowVersion, updated.RowVersion);
    }

    [Fact]
    public async Task The_route_id_wins_over_an_id_in_the_body()
    {
        using var admin = fixture.AsAdmin();
        var classroom = await TestData.ClassroomAsync(admin);
        var target = await TestData.CreateStudentAsync(admin, classroom.Id);
        var other = await TestData.CreateStudentAsync(admin, classroom.Id);

        var response = await admin.PutJsonAsync($"{Url}/{target.Id}", Update(target, name: "Via Route") with { Id = other.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Via Route", (await response.ReadAsync<StudentDto>()).FullName);
        Assert.Equal(other.FullName, (await (await admin.GetAsync($"{Url}/{other.Id}")).ReadAsync<StudentDto>()).FullName);
    }

    [Fact]
    public async Task An_update_based_on_an_old_read_is_rejected_instead_of_overwriting_a_newer_change()
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutJsonAsync($"{Url}/{student.Id}", Update(student, name: "First Editor"))).StatusCode);

        // The second editor still holds the row version of the original read.
        var stale = await admin.PutJsonAsync($"{Url}/{student.Id}", Update(student, name: "Second Editor"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());
        Assert.Equal("First Editor", (await (await admin.GetAsync($"{Url}/{student.Id}")).ReadAsync<StudentDto>()).FullName);
    }

    [Fact]
    public async Task Updating_to_the_nisn_of_another_student_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var classroom = await TestData.ClassroomAsync(admin);
        var first = await TestData.CreateStudentAsync(admin, classroom.Id);
        var second = await TestData.CreateStudentAsync(admin, classroom.Id);

        var response = await admin.PutJsonAsync($"{Url}/{second.Id}", Update(second, nisn: first.Nisn));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("students.nisn_taken", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_student_who_already_signed_in_has_their_name_and_email_managed_by_keycloak()
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);
        using (var asStudent = fixture.AsUser(student.Email, student.FullName, $"sub-{Guid.NewGuid():N}", "Student"))
        {
            Assert.Equal(HttpStatusCode.OK, (await asStudent.GetAsync("/api/v1/users/me")).StatusCode); // first login links the account
        }

        var linked = (await (await admin.GetAsync($"{Url}/{student.Id}")).ReadAsync<StudentDto>());
        Assert.True(linked.HasAccount);

        var rename = await admin.PutJsonAsync($"{Url}/{student.Id}", Update(linked, name: "Admin Renamed"));
        var otherDetails = await admin.PutJsonAsync($"{Url}/{student.Id}", Update(linked, nisn: TestData.NextNisn()));

        Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
        Assert.Equal("users.managed_by_keycloak", await rename.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, otherDetails.StatusCode); // the NISN and the classroom are still the school's to edit
    }

    [Fact]
    public async Task Updating_a_student_that_does_not_exist_is_not_found()
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);

        var response = await admin.PutJsonAsync($"{Url}/{Guid.NewGuid()}", Update(student));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Updating_with_a_missing_row_version_is_a_validation_error()
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);

        var response = await admin.PutJsonAsync($"{Url}/{student.Id}", Update(student, rowVersion: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    public async Task Only_admin_may_update_or_delete_students(string role)
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutJsonAsync($"{Url}/{student.Id}", Update(student))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Url}/{student.Id}")).StatusCode);
    }

    // ---- delete ----

    [Fact]
    public async Task Deleting_a_student_hides_them_everywhere_and_keeps_the_row_for_history()
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);

        var response = await admin.DeleteAsync($"{Url}/{student.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{student.Id}")).StatusCode);
        var list = await (await admin.GetAsync($"{Url}?search={Uri.EscapeDataString(student.Nisn)}")).ReadAsync<PagedResult<StudentDto>>();
        Assert.Empty(list.Items);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Students.IgnoreQueryFilters().AnyAsync(s => s.Id == student.Id && s.IsDeleted));
    }

    [Fact]
    public async Task The_nisn_and_email_of_a_deleted_student_can_be_used_again()
    {
        using var admin = fixture.AsAdmin();
        var classroom = await TestData.ClassroomAsync(admin);
        var student = await TestData.CreateStudentAsync(admin, classroom.Id);
        await admin.DeleteAsync($"{Url}/{student.Id}");

        var again = await admin.PostJsonAsync(Url, new CreateStudentCommand("Back Again", student.Email, student.Nisn, classroom.Id, new DateOnly(2013, 1, 2)));

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task A_deleted_student_who_had_signed_in_cannot_come_back_by_logging_in_again()
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);
        var sub = $"sub-{Guid.NewGuid():N}";
        using var asStudent = fixture.AsUser(student.Email, student.FullName, sub, "Student");
        Assert.Equal(HttpStatusCode.OK, (await asStudent.GetAsync("/api/v1/users/me")).StatusCode);

        await admin.DeleteAsync($"{Url}/{student.Id}");
        var afterwards = await asStudent.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.Forbidden, afterwards.StatusCode);
        Assert.Equal("auth.account_deactivated", await afterwards.ProblemCodeAsync());
    }

    [Fact]
    public async Task Deleting_a_student_also_removes_the_link_to_their_parents()
    {
        using var admin = fixture.AsAdmin();
        var student = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin)).Id);
        Guid parentId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            parentId = await db.Users.Where(u => u.Email == "parent@schomans.demo").Select(u => u.Id).SingleAsync();
            db.StudentParents.Add(SchoMans.Domain.Students.StudentParent.Create(parentId, student.Id));
            await db.SaveChangesAsync();
        }

        await admin.DeleteAsync($"{Url}/{student.Id}");

        await using var check = fixture.Factory.Services.CreateAsyncScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await checkDb.StudentParents.AnyAsync(p => p.StudentId == student.Id));
    }

    [Fact]
    public async Task Deleting_a_student_that_does_not_exist_is_not_found()
    {
        using var admin = fixture.AsAdmin();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }
}
