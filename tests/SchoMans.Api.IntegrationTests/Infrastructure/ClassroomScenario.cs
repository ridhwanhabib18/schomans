using SchoMans.Application.Common;
using SchoMans.Application.Features.Classrooms;
using SchoMans.Application.Features.Classrooms.CreateClassroom;
using SchoMans.Application.Features.Schedules;
using SchoMans.Application.Features.Schedules.CreateSchedule;
using SchoMans.Application.Features.Students;
using SchoMans.Application.Features.Subjects;
using SchoMans.Application.Features.Teachers;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A classroom of its own for one test: a new teacher (homeroom teacher and the teacher of one lesson), a classroom of
/// the current year, a few students, and one weekly lesson on a chosen day. Nothing collides with the demo timetable or
/// with other tests, and every person can sign in (the account is linked by email on the first request).
/// </summary>
public sealed class ClassroomScenario
{
    private readonly ApiFactory _factory;

    private ClassroomScenario(ApiFactory factory) => _factory = factory;

    public TeacherDto Teacher { get; private set; } = null!;

    public ClassroomDto Classroom { get; private set; } = null!;

    public SubjectDto Subject { get; private set; } = null!;

    public ScheduleDto Lesson { get; private set; } = null!;

    public IReadOnlyList<StudentDto> Students { get; private set; } = [];

    public HttpClient TeacherClient() => _factory.AsUser(Teacher.Email, Teacher.FullName, $"sub-{Teacher.Email}", "Teacher");

    public HttpClient StudentClient(int index) =>
        _factory.AsUser(Students[index].Email, Students[index].FullName, $"sub-{Students[index].Email}", "Student");

    /// <param name="day">The weekday of the lesson; today in WIB when not given.</param>
    public static async Task<ClassroomScenario> CreateAsync(
        ApiFactory factory,
        int studentCount = 3,
        DayOfWeek? day = null,
        TimeOnly? start = null,
        TimeOnly? end = null,
        string subjectName = "Mathematics")
    {
        using var admin = factory.AsUser("admin@schomans.demo", "Sari Utami", "sub-demo-admin", "Admin");
        var scenario = new ClassroomScenario(factory) { Teacher = await TestData.CreateTeacherAsync(admin) };

        var years = await (await admin.GetAsync("/api/v1/academic-years?pageSize=100")).ReadAsync<PagedResult<Application.Features.AcademicYears.AcademicYearDto>>();
        var year = years.Items.Single(y => y.IsActive);
        var created = await admin.PostJsonAsync(
            "/api/v1/classrooms",
            new CreateClassroomCommand($"T-{Guid.NewGuid():N}"[..20], year.Id, scenario.Teacher.Id));
        created.EnsureSuccessStatusCode();
        scenario.Classroom = await created.ReadAsync<ClassroomDto>();

        var subjects = await (await admin.GetAsync("/api/v1/subjects?pageSize=100")).ReadAsync<PagedResult<SubjectDto>>();
        scenario.Subject = subjects.Items.Single(s => s.Name == subjectName);

        var students = new List<StudentDto>();
        for (var i = 0; i < studentCount; i++)
        {
            students.Add(await TestData.CreateStudentAsync(admin, scenario.Classroom.Id));
        }

        scenario.Students = students;

        var lesson = await admin.PostJsonAsync("/api/v1/schedules", new CreateScheduleCommand(
            scenario.Classroom.Id,
            scenario.Subject.Id,
            scenario.Teacher.Id,
            day ?? TestClock.WibToday().DayOfWeek,
            start ?? new TimeOnly(7, 0),
            end ?? new TimeOnly(8, 30)));
        lesson.EnsureSuccessStatusCode();
        scenario.Lesson = await lesson.ReadAsync<ScheduleDto>();
        return scenario;
    }
}
