using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Schedules;
using SchoMans.Application.Features.Schedules.CreateSchedule;
using SchoMans.Application.Features.Schedules.UpdateSchedule;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class ScheduleEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/schedules";

    [Fact]
    public async Task The_demo_timetable_has_four_lessons_a_day_monday_to_friday_for_every_classroom()
    {
        using var teacher = fixture.AsTeacher();
        using var admin = fixture.AsAdmin();
        var classA = await TestData.ClassroomAsync(admin, "Class A");

        var page = await (await teacher.GetAsync($"{Url}?classroomId={classA.Id}&pageSize=100")).ReadAsync<PagedResult<ScheduleDto>>();

        Assert.Equal(20, page.Items.Count(s => s.StartTime < new TimeOnly(15, 0)));
        Assert.Equal(DayOfWeek.Monday, page.Items[0].DayOfWeek); // the school week starts on Monday
        Assert.Equal(new TimeOnly(7, 0), page.Items[0].StartTime);
    }

    [Fact]
    public async Task The_timetable_can_be_filtered_by_teacher_and_day()
    {
        using var admin = fixture.AsAdmin();
        var classA = await TestData.ClassroomAsync(admin, "Class A");

        var page = await (await admin.GetAsync($"{Url}?teacherId={classA.HomeroomTeacherId}&dayOfWeek=Monday&pageSize=100")).ReadAsync<PagedResult<ScheduleDto>>();

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, s =>
        {
            Assert.Equal(classA.HomeroomTeacherId, s.TeacherId);
            Assert.Equal(DayOfWeek.Monday, s.DayOfWeek);
            Assert.Equal("Mathematics", s.SubjectName);
        });
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Applicant")]
    public async Task Only_staff_see_the_timetable(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Admin_adds_moves_and_removes_a_lesson()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, studentCount: 0, day: DayOfWeek.Saturday, start: new TimeOnly(9, 0), end: new TimeOnly(10, 0));
        using var admin = fixture.AsAdmin();

        var created = await admin.PostJsonAsync(Url, new CreateScheduleCommand(
            scenario.Classroom.Id, scenario.Subject.Id, scenario.Teacher.Id, DayOfWeek.Saturday, new TimeOnly(10, 0), new TimeOnly(11, 0)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var lesson = await created.ReadAsync<ScheduleDto>();
        Assert.Equal(scenario.Classroom.Name, lesson.ClassroomName);
        Assert.Equal(scenario.Teacher.FullName, lesson.TeacherName);

        var moved = await admin.PutJsonAsync($"{Url}/{lesson.Id}", new UpdateScheduleCommand(
            lesson.Id, scenario.Subject.Id, scenario.Teacher.Id, DayOfWeek.Sunday, new TimeOnly(8, 0), new TimeOnly(9, 0), lesson.RowVersion));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(DayOfWeek.Sunday, (await moved.ReadAsync<ScheduleDto>()).DayOfWeek);

        var stale = await admin.PutJsonAsync($"{Url}/{lesson.Id}", new UpdateScheduleCommand(
            lesson.Id, scenario.Subject.Id, scenario.Teacher.Id, DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(10, 0), lesson.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Url}/{lesson.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_classroom_cannot_have_two_lessons_at_once()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, studentCount: 0, day: DayOfWeek.Saturday, start: new TimeOnly(9, 0), end: new TimeOnly(10, 0));
        using var admin = fixture.AsAdmin();
        var other = await TestData.CreateTeacherAsync(admin);

        var response = await admin.PostJsonAsync(Url, new CreateScheduleCommand(
            scenario.Classroom.Id, scenario.Subject.Id, other.Id, DayOfWeek.Saturday, new TimeOnly(9, 30), new TimeOnly(10, 30)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("schedules.classroom_busy", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_teacher_cannot_teach_two_lessons_at_once()
    {
        var first = await ClassroomScenario.CreateAsync(fixture.Factory, studentCount: 0, day: DayOfWeek.Saturday, start: new TimeOnly(9, 0), end: new TimeOnly(10, 0));
        var second = await ClassroomScenario.CreateAsync(fixture.Factory, studentCount: 0, day: DayOfWeek.Sunday);
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateScheduleCommand(
            second.Classroom.Id, second.Subject.Id, first.Teacher.Id, DayOfWeek.Saturday, new TimeOnly(8, 30), new TimeOnly(9, 30)));
        var touching = await admin.PostJsonAsync(Url, new CreateScheduleCommand(
            second.Classroom.Id, second.Subject.Id, first.Teacher.Id, DayOfWeek.Saturday, new TimeOnly(10, 0), new TimeOnly(11, 0)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("schedules.teacher_busy", await response.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Created, touching.StatusCode); // back-to-back lessons are fine
    }

    [Fact]
    public async Task A_lesson_must_end_after_it_starts()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateScheduleCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(8, 0)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_lesson_for_an_unknown_classroom_is_not_found()
    {
        using var admin = fixture.AsAdmin();
        var classA = await TestData.ClassroomAsync(admin, "Class A");

        var response = await admin.PostJsonAsync(Url, new CreateScheduleCommand(
            Guid.NewGuid(), Guid.NewGuid(), classA.HomeroomTeacherId, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(10, 0)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("classrooms.not_found", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Principal")]
    public async Task Only_admin_staff_change_the_timetable(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.PostJsonAsync(Url, new CreateScheduleCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_teacher_with_lessons_and_a_subject_in_the_timetable_cannot_be_deleted()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, studentCount: 0, day: DayOfWeek.Saturday);
        using var admin = fixture.AsAdmin();
        var otherTeacher = await TestData.CreateTeacherAsync(admin);
        var lessonOnly = await admin.PostJsonAsync(Url, new CreateScheduleCommand(
            scenario.Classroom.Id, scenario.Subject.Id, otherTeacher.Id, DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(10, 0)));
        lessonOnly.EnsureSuccessStatusCode();

        var teacher = await admin.DeleteAsync($"/api/v1/teachers/{otherTeacher.Id}");
        var subject = await admin.DeleteAsync($"/api/v1/subjects/{scenario.Subject.Id}");
        var classroom = await admin.DeleteAsync($"/api/v1/classrooms/{scenario.Classroom.Id}");

        Assert.Equal("teachers.has_lessons", await teacher.ProblemCodeAsync());
        Assert.Equal("subjects.in_use", await subject.ProblemCodeAsync());
        Assert.Equal("classrooms.has_lessons", await classroom.ProblemCodeAsync());
    }
}
