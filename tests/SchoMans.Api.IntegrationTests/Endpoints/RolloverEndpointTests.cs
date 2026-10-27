using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.AcademicYears;
using SchoMans.Application.Features.AcademicYears.RolloverAcademicYear;
using SchoMans.Application.Features.Classrooms;
using SchoMans.Application.Features.Students;
using SchoMans.Application.Features.Students.UpdateStudent;
using SchoMans.Application.Features.Teachers;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// The rollover changes global state (the active year, every student), so each test gets a database of its own:
/// the fixture is created and dropped per test instead of being shared.
/// </summary>
public class RolloverEndpointTests : IAsyncLifetime
{
    private const string Url = "/api/v1/academic-years/rollover";

    private readonly ApiFixture _fixture = new();
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _admin = _fixture.AsAdmin();
    }

    public async Task DisposeAsync()
    {
        _admin.Dispose();
        await _fixture.DisposeAsync();
    }

    private async Task<List<ClassroomDto>> ClassroomsOfActiveYearAsync()
    {
        var years = (await (await _admin.GetAsync("/api/v1/academic-years")).ReadAsync<PagedResult<AcademicYearDto>>()).Items;
        var active = years.Single(y => y.IsActive);
        var page = await (await _admin.GetAsync($"/api/v1/classrooms?academicYearId={active.Id}&pageSize=100&sort=name")).ReadAsync<PagedResult<ClassroomDto>>();
        return page.Items.ToList();
    }

    private async Task<Guid> AnyTeacherAsync() =>
        (await (await _admin.GetAsync("/api/v1/teachers?pageSize=100")).ReadAsync<PagedResult<TeacherDto>>()).Items.Last().Id;

    private async Task<PagedResult<StudentDto>> StudentsAsync(string query) =>
        await (await _admin.GetAsync($"/api/v1/students?{query}")).ReadAsync<PagedResult<StudentDto>>();

    private static RolloverAcademicYearCommand Command(string name, IReadOnlyList<RolloverMapping> mappings) => new(
        name,
        new DateOnly(2027, 7, 12),
        new DateOnly(2028, 6, 17),
        [new TermInput(null, "Term 1", new DateOnly(2027, 7, 12), new DateOnly(2027, 12, 18)), new TermInput(null, "Term 2", new DateOnly(2028, 1, 3), new DateOnly(2028, 6, 17))],
        mappings);

    private static RolloverMapping Move(ClassroomDto from, string to, Guid teacher) => new(from.Id, Graduate: false, to, teacher);

    private static RolloverMapping Graduate(ClassroomDto from) => new(from.Id, Graduate: true, null, null);

    [Fact]
    public async Task The_whole_school_moves_up_merges_and_graduates_in_one_step()
    {
        var classrooms = await ClassroomsOfActiveYearAsync(); // Class A..E
        var teacher = await AnyTeacherAsync();
        var oldYear = (await (await _admin.GetAsync("/api/v1/academic-years")).ReadAsync<PagedResult<AcademicYearDto>>()).Items.Single(y => y.IsActive);

        var response = await _admin.PostJsonAsync(Url, Command("2027/2028",
        [
            Move(classrooms[0], "Next A", teacher),
            Move(classrooms[1], "Next B", teacher),
            Move(classrooms[2], "next b", teacher), // two classes merged into one
            Graduate(classrooms[3]),
            Graduate(classrooms[4]),
        ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<RolloverResult>();
        Assert.Equal(90, result.MovedStudents);
        Assert.Equal(60, result.GraduatedStudents);
        Assert.Equal(2, result.CreatedClassrooms);

        var years = (await (await _admin.GetAsync("/api/v1/academic-years")).ReadAsync<PagedResult<AcademicYearDto>>()).Items;
        Assert.Equal([result.AcademicYearId], years.Where(y => y.IsActive).Select(y => y.Id));
        Assert.False(years.Single(y => y.Id == oldYear.Id).IsActive);
        Assert.Equal(2, years.Single(y => y.Id == result.AcademicYearId).Terms.Count);

        var newClassrooms = await ClassroomsOfActiveYearAsync();
        Assert.Equal(["Next A", "Next B"], newClassrooms.Select(c => c.Name));
        Assert.Equal([30, 60], newClassrooms.Select(c => c.StudentCount));

        var active = await StudentsAsync("status=Active&pageSize=100");
        Assert.Equal(90, active.TotalCount);
        Assert.All(active.Items, s => Assert.StartsWith("Next", s.ClassroomName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Graduated_students_keep_their_last_classroom_and_cannot_be_moved()
    {
        var classrooms = await ClassroomsOfActiveYearAsync();
        var teacher = await AnyTeacherAsync();
        await _admin.PostJsonAsync(Url, Command("2027/2028",
        [
            Move(classrooms[0], "Next A", teacher), Move(classrooms[1], "Next A", teacher), Move(classrooms[2], "Next A", teacher),
            Move(classrooms[3], "Next A", teacher), Graduate(classrooms[4]),
        ]));

        var graduated = await StudentsAsync("status=Graduated&pageSize=100");
        var student = graduated.Items[0];

        Assert.Equal(30, graduated.TotalCount);
        Assert.Equal("Class E", student.ClassroomName);
        Assert.Equal("Graduated", student.Status);

        var next = (await ClassroomsOfActiveYearAsync()).Single();
        var move = await _admin.PutJsonAsync($"/api/v1/students/{student.Id}", new UpdateStudentCommand(
            student.Id, student.FullName, student.Email, student.Nisn, next.Id, student.DateOfBirth, student.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
        Assert.Equal("students.graduated", await move.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_second_rollover_works_and_leaves_earlier_graduates_alone()
    {
        var teacher = await AnyTeacherAsync();
        var first = await ClassroomsOfActiveYearAsync();
        await _admin.PostJsonAsync(Url, Command("2027/2028",
        [
            Move(first[0], "Year Two", teacher), Move(first[1], "Year Two", teacher), Move(first[2], "Year Two", teacher),
            Move(first[3], "Year Two", teacher), Graduate(first[4]),
        ]));
        var second = await ClassroomsOfActiveYearAsync();

        var response = await _admin.PostJsonAsync(Url, new RolloverAcademicYearCommand(
            "2028/2029",
            new DateOnly(2028, 7, 10),
            new DateOnly(2029, 6, 16),
            [new TermInput(null, "Term 1", new DateOnly(2028, 7, 10), new DateOnly(2028, 12, 16))],
            [Graduate(second.Single())]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<RolloverResult>();
        Assert.Equal(120, result.GraduatedStudents);
        Assert.Equal(0, result.CreatedClassrooms);
        Assert.Equal(150, (await StudentsAsync("status=Graduated&pageSize=1")).TotalCount);
        Assert.Equal(0, (await StudentsAsync("status=Active&pageSize=1")).TotalCount);
    }

    [Fact]
    public async Task Every_classroom_of_the_current_year_must_be_mapped_and_nothing_changes_otherwise()
    {
        var classrooms = await ClassroomsOfActiveYearAsync();
        var teacher = await AnyTeacherAsync();

        var response = await _admin.PostJsonAsync(Url, Command("2027/2028",
        [
            Move(classrooms[0], "Next A", teacher), Move(classrooms[1], "Next A", teacher), Move(classrooms[2], "Next A", teacher),
            Graduate(classrooms[3]), // Class E is forgotten
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("academic_years.rollover_mapping_incomplete", await response.ProblemCodeAsync());
        Assert.Equal(5, (await ClassroomsOfActiveYearAsync()).Count);
        Assert.Equal(150, (await StudentsAsync("status=Active&pageSize=1")).TotalCount);
    }

    [Fact]
    public async Task A_classroom_cannot_be_mapped_twice_or_one_of_another_year_be_mapped_at_all()
    {
        var classrooms = await ClassroomsOfActiveYearAsync();
        var teacher = await AnyTeacherAsync();
        var all = classrooms.Select(c => Graduate(c)).ToList();

        var twice = await _admin.PostJsonAsync(Url, Command("2027/2028", [.. all, Graduate(classrooms[0])]));
        var stranger = await _admin.PostJsonAsync(Url, Command("2027/2028", [.. all.Skip(1), new RolloverMapping(Guid.NewGuid(), true, null, null)]));
        _ = teacher;

        Assert.Equal("academic_years.rollover_mapping_incomplete", await twice.ProblemCodeAsync());
        Assert.Equal("academic_years.rollover_mapping_incomplete", await stranger.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_new_classroom_needs_a_homeroom_teacher_who_exists_and_nothing_changes_otherwise()
    {
        var classrooms = await ClassroomsOfActiveYearAsync();
        var mappings = classrooms.Select(c => Move(c, "Next", Guid.NewGuid())).ToList();

        var response = await _admin.PostJsonAsync(Url, Command("2027/2028", mappings));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("teachers.not_found", await response.ProblemCodeAsync());
        Assert.Equal(5, (await ClassroomsOfActiveYearAsync()).Count);
    }

    [Fact]
    public async Task The_new_year_needs_a_new_name_and_must_start_after_the_current_one()
    {
        var classrooms = await ClassroomsOfActiveYearAsync();
        var mappings = classrooms.Select(c => Graduate(c)).ToList();

        var sameName = await _admin.PostJsonAsync(Url, Command("2026/2027", mappings));
        var tooEarly = await _admin.PostJsonAsync(Url, Command("2025/2026", mappings) with { StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2027, 6, 1),
            Terms = [new TermInput(null, "Term 1", new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 1))] });

        Assert.Equal(HttpStatusCode.Conflict, sameName.StatusCode);
        Assert.Equal("academic_years.name_taken", await sameName.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        Assert.Equal("academic_years.rollover_dates", await tooEarly.ProblemCodeAsync());
    }

    [Fact]
    public async Task An_incomplete_request_is_rejected_by_validation()
    {
        var response = await _admin.PostJsonAsync(Url, Command("2027/2028", [new RolloverMapping(Guid.NewGuid(), false, null, null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    public async Task Only_admin_may_roll_the_school_over(string role)
    {
        using var client = _fixture.AsNewUser(role);

        var response = await client.PostJsonAsync(Url, Command("2027/2028", [Graduate(new ClassroomDto(Guid.NewGuid(), "x", Guid.NewGuid(), "y", Guid.NewGuid(), "z", 0, "v"))]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
