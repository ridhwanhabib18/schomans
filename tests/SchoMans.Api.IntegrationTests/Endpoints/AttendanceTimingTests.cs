using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.Hubs;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Attendance;
using SchoMans.Application.Features.Attendance.CloseStaleSessions;
using SchoMans.Application.Features.Attendance.OpenSession;
using SchoMans.Application.Features.Attendance.RotateQrCodes;
using SchoMans.Application.Features.Attendance.ScanQr;
using SchoMans.Domain.Attendance;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// The parts of QR attendance that depend on time and on the live screen: the code rotates every minute with a 10 second
/// grace, forgotten sessions close themselves, the location check, and the SignalR events of the session screen.
/// Each test runs a second API host on the shared database with a clock it controls.
/// </summary>
[Collection(ApiCollection.Name)]
public class AttendanceTimingTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/attendance";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    // A Wednesday at 07:05 WIB, during the first lesson of the scenario (07:00 to 08:30).
    private static readonly DateOnly Day = new(2026, 10, 28);

    private static TestClock LessonClock() => new(TestClock.Wib(Day, new TimeOnly(7, 5)));

    private static async Task<AttendanceSessionDto> OpenAsync(ClassroomScenario scenario)
    {
        using var teacher = scenario.TeacherClient();
        var response = await teacher.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(scenario.Lesson.Id));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<AttendanceSessionDto>();
    }

    private static async Task<int> RunAsync<THandler>(ApiFactory factory, Func<THandler, Task<int>> run)
        where THandler : notnull
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await run(scope.ServiceProvider.GetRequiredService<THandler>());
    }

    [Fact]
    public async Task After_a_rotation_the_old_code_works_for_ten_seconds_then_only_the_new_one()
    {
        var clock = LessonClock();
        await using var factory = fixture.CreateFactory(clock);
        var scenario = await ClassroomScenario.CreateAsync(factory, studentCount: 3, day: Day.DayOfWeek);
        var session = await OpenAsync(scenario);
        var oldToken = session.Qr!.Token;

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await RunAsync<RotateQrCodesHandler>(factory, h => h.Handle(CancellationToken.None)) >= 1);
        using var teacher = scenario.TeacherClient();
        var newToken = (await (await teacher.GetAsync($"{Url}/sessions/{session.Id}")).ReadAsync<AttendanceSessionDto>()).Qr!.Token;

        clock.Advance(TimeSpan.FromSeconds(10));
        using var atChangeover = scenario.StudentClient(0);
        var withinGrace = await atChangeover.PostJsonAsync($"{Url}/scans", new ScanQrCommand(oldToken));

        clock.Advance(TimeSpan.FromSeconds(1));
        using var photoAtHome = scenario.StudentClient(1);
        var afterGrace = await photoAtHome.PostJsonAsync($"{Url}/scans", new ScanQrCommand(oldToken));
        using var inClass = scenario.StudentClient(2);
        var current = await inClass.PostJsonAsync($"{Url}/scans", new ScanQrCommand(newToken));

        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(HttpStatusCode.OK, withinGrace.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, afterGrace.StatusCode);
        Assert.Equal("attendance.qr_expired", await afterGrace.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }

    [Fact]
    public async Task A_forgotten_session_closes_a_quarter_hour_after_the_lesson_and_marks_the_missing_absent()
    {
        var clock = LessonClock();
        await using var factory = fixture.CreateFactory(clock);
        var scenario = await ClassroomScenario.CreateAsync(factory, studentCount: 2, day: Day.DayOfWeek);
        var session = await OpenAsync(scenario);
        using var student = scenario.StudentClient(0);
        (await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr!.Token))).EnsureSuccessStatusCode();

        clock.Set(TestClock.Wib(Day, new TimeOnly(8, 44))); // the lesson ended at 08:30
        await RunAsync<CloseStaleSessionsHandler>(factory, h => h.Handle(CancellationToken.None));
        using var teacher = scenario.TeacherClient();
        var stillOpen = await (await teacher.GetAsync($"{Url}/sessions/{session.Id}")).ReadAsync<AttendanceSessionDto>();

        clock.Set(TestClock.Wib(Day, new TimeOnly(8, 45)));
        await RunAsync<CloseStaleSessionsHandler>(factory, h => h.Handle(CancellationToken.None));
        var closed = await (await teacher.GetAsync($"{Url}/sessions/{session.Id}")).ReadAsync<AttendanceSessionDto>();

        Assert.True(stillOpen.IsOpen);
        Assert.False(closed.IsOpen);
        Assert.Equal(AttendanceStatus.Present, closed.Students.Single(s => s.StudentId == scenario.Students[0].Id).Status);
        Assert.Equal(AttendanceStatus.Absent, closed.Students.Single(s => s.StudentId == scenario.Students[1].Id).Status);
    }

    [Fact]
    public async Task A_session_opened_after_its_lesson_stays_open_for_at_least_half_an_hour()
    {
        var clock = new TestClock(TestClock.Wib(Day, new TimeOnly(9, 0))); // after the 07:00 to 08:30 lesson
        await using var factory = fixture.CreateFactory(clock);
        var scenario = await ClassroomScenario.CreateAsync(factory, studentCount: 1, day: Day.DayOfWeek);
        var session = await OpenAsync(scenario);
        using var teacher = scenario.TeacherClient();

        clock.Advance(TimeSpan.FromMinutes(29));
        await RunAsync<CloseStaleSessionsHandler>(factory, h => h.Handle(CancellationToken.None));
        var after29 = await (await teacher.GetAsync($"{Url}/sessions/{session.Id}")).ReadAsync<AttendanceSessionDto>();
        clock.Advance(TimeSpan.FromMinutes(1));
        await RunAsync<CloseStaleSessionsHandler>(factory, h => h.Handle(CancellationToken.None));
        var after30 = await (await teacher.GetAsync($"{Url}/sessions/{session.Id}")).ReadAsync<AttendanceSessionDto>();

        Assert.True(after29.IsOpen);
        Assert.False(after30.IsOpen);
    }

    [Fact]
    public async Task With_the_location_check_on_a_scan_counts_only_at_school()
    {
        var clock = LessonClock();
        await using var factory = fixture.CreateFactory(clock, new Dictionary<string, string>
        {
            ["Attendance:Geofence:Enabled"] = "true",
            ["Attendance:Geofence:Latitude"] = "-6.2",
            ["Attendance:Geofence:Longitude"] = "106.8",
            ["Attendance:Geofence:RadiusMeters"] = "200",
        });
        var scenario = await ClassroomScenario.CreateAsync(factory, studentCount: 3, day: Day.DayOfWeek);
        var session = await OpenAsync(scenario);
        using var noLocation = scenario.StudentClient(0);
        using var atHome = scenario.StudentClient(1);
        using var inClass = scenario.StudentClient(2);

        var settings = await (await inClass.GetAsync($"{Url}/scan-settings")).ReadAsync<ScanSettingsDto>();
        var missing = await noLocation.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr!.Token));
        var outside = await atHome.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr.Token, -6.25, 106.8));
        var inside = await inClass.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr.Token, -6.2005, 106.8003));

        Assert.True(settings.GeofenceEnabled);
        Assert.Equal(200, settings.RadiusMeters);
        Assert.Equal("attendance.location_required", await missing.ProblemCodeAsync());
        Assert.Equal("attendance.outside_school", await outside.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, inside.StatusCode);
    }

    [Fact]
    public async Task The_session_screen_receives_check_ins_new_codes_and_the_closing_live()
    {
        var clock = LessonClock();
        await using var factory = fixture.CreateFactory(clock);
        var scenario = await ClassroomScenario.CreateAsync(factory, studentCount: 1, day: Day.DayOfWeek);
        var session = await OpenAsync(scenario);
        var email = scenario.Teacher.Email;
        await using var screen = await HubTestClient.ConnectToAsync(factory, AttendanceHub.Path, email, scenario.Teacher.FullName, $"sub-{email}", "Teacher");
        await using var principal = await HubTestClient.ConnectToAsync(factory, AttendanceHub.Path, "principal@schomans.demo", "Hendra Wirawan", "sub-demo-principal", "Principal");

        Assert.Null(await screen.InvokeAsync("JoinSession", session.Id));
        Assert.Null(await principal.InvokeAsync("JoinSession", session.Id));

        using var student = scenario.StudentClient(0);
        (await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr!.Token))).EnsureSuccessStatusCode();
        var checkedIn = await screen.WaitForInvocationAsync(AttendanceHub.StudentCheckedInEvent, Wait);

        clock.Advance(TimeSpan.FromMinutes(1));
        await RunAsync<RotateQrCodesHandler>(factory, h => h.Handle(CancellationToken.None));
        var rotated = await screen.WaitForInvocationAsync(AttendanceHub.QrRotatedEvent, Wait);
        var principalGotCode = await principal.WaitForInvocationAsync(AttendanceHub.QrRotatedEvent, TimeSpan.FromMilliseconds(800));

        using var teacher = scenario.TeacherClient();
        (await teacher.PostAsync($"{Url}/sessions/{session.Id}/close", null)).EnsureSuccessStatusCode();
        var closed = await screen.WaitForInvocationAsync(AttendanceHub.SessionClosedEvent, Wait);

        Assert.NotNull(checkedIn);
        Assert.Equal(scenario.Students[0].Id, checkedIn.Value.GetProperty("studentId").GetGuid());
        Assert.Equal("Present", checkedIn.Value.GetProperty("status").GetString());
        Assert.NotNull(rotated);
        Assert.NotEqual(session.Qr.Token, rotated.Value.GetProperty("token").GetString());
        Assert.Null(principalGotCode); // the code goes only to the teacher's screen
        Assert.NotNull(closed);
    }

    [Fact]
    public async Task Another_teacher_cannot_watch_a_session_and_students_cannot_connect()
    {
        var clock = LessonClock();
        await using var factory = fixture.CreateFactory(clock);
        var scenario = await ClassroomScenario.CreateAsync(factory, studentCount: 0, day: Day.DayOfWeek);
        var session = await OpenAsync(scenario);
        await using var otherTeacher = await HubTestClient.ConnectToAsync(factory, AttendanceHub.Path, "teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");

        var error = await otherTeacher.InvokeAsync("JoinSession", session.Id);

        Assert.Contains("attendance.not_your_lesson", error);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            HubTestClient.ConnectToAsync(factory, AttendanceHub.Path, "student@schomans.demo", "Rina Putri", "sub-demo-student", "Student"));
    }
}
