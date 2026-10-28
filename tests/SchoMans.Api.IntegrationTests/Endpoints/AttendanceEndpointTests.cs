using System.Net;
using ClosedXML.Excel;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Attendance;
using SchoMans.Application.Features.Attendance.MarkAttendance;
using SchoMans.Application.Features.Attendance.OpenSession;
using SchoMans.Application.Features.Attendance.ScanQr;
using SchoMans.Domain.Attendance;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>"Attendance flows pass": the teacher opens a lesson, students scan the QR code, the teacher closes it.</summary>
[Collection(ApiCollection.Name)]
public class AttendanceEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/attendance";

    private static string Month => TestClock.WibToday().ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<(ClassroomScenario Scenario, AttendanceSessionDto Session)> OpenAsync(int students = 3)
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, students);
        using var teacher = scenario.TeacherClient();
        var response = await teacher.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(scenario.Lesson.Id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (scenario, await response.ReadAsync<AttendanceSessionDto>());
    }

    [Fact]
    public async Task The_teacher_sees_today_s_lessons_with_the_state_of_their_attendance()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, studentCount: 2);
        using var teacher = scenario.TeacherClient();

        var before = await (await teacher.GetAsync($"{Url}/today")).ReadAsync<List<TodayLessonDto>>();
        (await teacher.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(scenario.Lesson.Id))).EnsureSuccessStatusCode();
        var after = await (await teacher.GetAsync($"{Url}/today")).ReadAsync<List<TodayLessonDto>>();

        var lesson = Assert.Single(before);
        Assert.Equal(scenario.Classroom.Name, lesson.ClassroomName);
        Assert.Null(lesson.SessionId);
        Assert.Equal(2, lesson.StudentCount);
        Assert.True(Assert.Single(after).SessionOpen);
    }

    [Fact]
    public async Task Opening_a_lesson_gives_the_teacher_a_qr_code_and_the_class_list()
    {
        var (scenario, session) = await OpenAsync(students: 3);

        Assert.True(session.IsOpen);
        Assert.NotNull(session.Qr);
        Assert.True(AttendanceSession.TryParseToken(session.Qr.Token, out var id, out _));
        Assert.Equal(session.Id, id);
        Assert.Equal(session.Qr.RotatedAt.AddMinutes(1), session.Qr.NextRotationAt);
        Assert.Equal(3, session.Students.Count);
        Assert.All(session.Students, s => Assert.Null(s.Status));
        Assert.Equal(scenario.Subject.Name, session.SubjectName);
        Assert.Equal(TestClock.WibToday(), session.Date);
    }

    [Fact]
    public async Task Opening_the_same_lesson_again_returns_the_same_session()
    {
        var (scenario, session) = await OpenAsync();
        using var teacher = scenario.TeacherClient();

        var again = await (await teacher.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(scenario.Lesson.Id))).ReadAsync<AttendanceSessionDto>();

        Assert.Equal(session.Id, again.Id);
    }

    [Fact]
    public async Task A_teacher_cannot_open_another_teacher_s_lesson_or_a_lesson_of_another_day()
    {
        var (scenario, _) = await OpenAsync();
        var otherDay = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: TestClock.WibToday().AddDays(1).DayOfWeek);
        using var otherTeacher = otherDay.TeacherClient();

        var foreign = await otherTeacher.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(scenario.Lesson.Id));
        var tomorrow = await otherTeacher.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(otherDay.Lesson.Id));

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal("attendance.not_your_lesson", await foreign.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, tomorrow.StatusCode);
        Assert.Equal("attendance.not_today", await tomorrow.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("Parent")]
    public async Task Only_teachers_open_sessions(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Opening_needs_a_lesson()
    {
        using var teacher = fixture.AsTeacher();

        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(Guid.Empty))).StatusCode);
    }

    [Fact]
    public async Task A_student_checks_in_by_scanning_and_a_second_scan_changes_nothing()
    {
        var (scenario, session) = await OpenAsync();
        using var student = scenario.StudentClient(0);

        var first = await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr!.Token));
        var second = await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr.Token));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = await first.ReadAsync<ScanResultDto>();
        Assert.Equal(AttendanceStatus.Present, result.Status);
        Assert.False(result.AlreadyRecorded);
        Assert.Equal(scenario.Classroom.Name, result.ClassroomName);
        var repeat = await second.ReadAsync<ScanResultDto>();
        Assert.True(repeat.AlreadyRecorded);
        Assert.Equal(result.ScannedAt, repeat.ScannedAt);

        using var teacher = scenario.TeacherClient();
        var view = await (await teacher.GetAsync($"{Url}/sessions/{session.Id}")).ReadAsync<AttendanceSessionDto>();
        Assert.Equal(AttendanceStatus.Present, view.Students.Single(s => s.StudentId == scenario.Students[0].Id).Status);
    }

    [Fact]
    public async Task A_student_of_another_classroom_cannot_use_the_code()
    {
        var (_, session) = await OpenAsync();
        using var stranger = fixture.AsStudent(); // Rina Putri of Class A

        var response = await stranger.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr!.Token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("attendance.not_in_classroom", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_made_up_or_wrong_code_is_rejected()
    {
        var (scenario, session) = await OpenAsync();
        using var student = scenario.StudentClient(0);

        var garbage = await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand("hello"));
        var unknown = await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(AttendanceSession.FormatToken(Guid.NewGuid(), "x")));
        var wrongSecret = await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(AttendanceSession.FormatToken(session.Id, "guessed")));

        Assert.Equal("attendance.qr_invalid", await garbage.ProblemCodeAsync());
        Assert.Equal("attendance.qr_invalid", await unknown.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, wrongSecret.StatusCode);
        Assert.Equal("attendance.qr_expired", await wrongSecret.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Parent")]
    public async Task Only_students_scan(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync($"{Url}/scans", new ScanQrCommand("x.y"))).StatusCode);
    }

    [Fact]
    public async Task A_scan_without_a_token_is_a_validation_error()
    {
        using var student = fixture.AsStudent();

        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(""))).StatusCode);
    }

    [Fact]
    public async Task Closing_marks_everybody_who_did_not_scan_absent_and_the_code_stops_working()
    {
        var (scenario, session) = await OpenAsync(students: 3);
        using var teacher = scenario.TeacherClient();
        using var present = scenario.StudentClient(0);
        using var late = scenario.StudentClient(1);
        (await present.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr!.Token))).EnsureSuccessStatusCode();

        var closed = await (await teacher.PostAsync($"{Url}/sessions/{session.Id}/close", null)).ReadAsync<AttendanceSessionDto>();
        var afterClose = await late.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr.Token));
        var closeAgain = await teacher.PostAsync($"{Url}/sessions/{session.Id}/close", null);
        var reopen = await teacher.PostJsonAsync($"{Url}/sessions", new OpenSessionCommand(scenario.Lesson.Id));

        Assert.False(closed.IsOpen);
        Assert.Null(closed.Qr);
        Assert.Equal(1, closed.Students.Count(s => s.Status == AttendanceStatus.Present));
        Assert.Equal(2, closed.Students.Count(s => s.Status == AttendanceStatus.Absent));
        // The late student's scan now only reports the Absent the closing recorded.
        Assert.Equal(AttendanceStatus.Absent, (await afterClose.ReadAsync<ScanResultDto>()).Status);
        Assert.Equal("attendance.session_closed", await closeAgain.ProblemCodeAsync());
        Assert.Equal("attendance.session_already_closed", await reopen.ProblemCodeAsync());
    }

    [Fact]
    public async Task The_teacher_marks_a_student_by_hand_while_open_and_corrects_absent_to_sick_after_closing()
    {
        var (scenario, session) = await OpenAsync(students: 2);
        using var teacher = scenario.TeacherClient();
        var noCamera = scenario.Students[0].Id;
        var ill = scenario.Students[1].Id;

        var manual = await (await teacher.PutJsonAsync($"{Url}/sessions/{session.Id}/students/{noCamera}", new MarkAttendanceCommand(session.Id, noCamera, AttendanceStatus.Present, "No camera"))).ReadAsync<SessionStudentDto>();
        (await teacher.PostAsync($"{Url}/sessions/{session.Id}/close", null)).EnsureSuccessStatusCode();
        var corrected = await (await teacher.PutJsonAsync($"{Url}/sessions/{session.Id}/students/{ill}", new MarkAttendanceCommand(session.Id, ill, AttendanceStatus.Sick, "Letter from home"))).ReadAsync<SessionStudentDto>();

        Assert.Equal(AttendanceStatus.Present, manual.Status);
        Assert.True(manual.IsManual);
        Assert.Null(manual.ScannedAt);
        Assert.Equal(AttendanceStatus.Sick, corrected.Status);
        Assert.Equal("Letter from home", corrected.Note);
    }

    [Fact]
    public async Task Marking_a_student_who_is_not_in_the_lesson_is_not_found_and_other_teachers_may_not_mark()
    {
        var (scenario, session) = await OpenAsync(students: 1);
        using var teacher = scenario.TeacherClient();
        using var otherTeacher = fixture.AsTeacher();
        using var admin = fixture.AsAdmin();
        var outsider = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin, "Class B")).Id);

        var notInLesson = await teacher.PutJsonAsync($"{Url}/sessions/{session.Id}/students/{outsider.Id}", new MarkAttendanceCommand(session.Id, outsider.Id, AttendanceStatus.Present, null));
        var foreign = await otherTeacher.PutJsonAsync($"{Url}/sessions/{session.Id}/students/{scenario.Students[0].Id}", new MarkAttendanceCommand(session.Id, scenario.Students[0].Id, AttendanceStatus.Absent, null));
        var tooLong = await teacher.PutJsonAsync($"{Url}/sessions/{session.Id}/students/{scenario.Students[0].Id}", new MarkAttendanceCommand(session.Id, scenario.Students[0].Id, AttendanceStatus.Sick, new string('x', 301)));

        Assert.Equal(HttpStatusCode.NotFound, notInLesson.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Admin_and_principal_see_a_session_without_its_qr_code_and_other_teachers_see_nothing()
    {
        var (_, session) = await OpenAsync();
        using var admin = fixture.AsAdmin();
        using var principal = fixture.AsPrincipal();
        using var otherTeacher = fixture.AsTeacher();

        var adminView = await (await admin.GetAsync($"{Url}/sessions/{session.Id}")).ReadAsync<AttendanceSessionDto>();
        var principalView = await (await principal.GetAsync($"{Url}/sessions/{session.Id}")).ReadAsync<AttendanceSessionDto>();

        Assert.Null(adminView.Qr);
        Assert.Null(principalView.Qr);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherTeacher.GetAsync($"{Url}/sessions/{session.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/sessions/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task The_month_summary_counts_each_status_per_student_and_exports_to_excel()
    {
        var (scenario, session) = await OpenAsync(students: 2);
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(0);
        (await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr!.Token))).EnsureSuccessStatusCode();
        (await teacher.PostAsync($"{Url}/sessions/{session.Id}/close", null)).EnsureSuccessStatusCode();

        var summary = await (await teacher.GetAsync($"{Url}/classrooms/{scenario.Classroom.Id}/summary?month={Month}")).ReadAsync<ClassroomAttendanceSummaryDto>();
        var export = await teacher.GetAsync($"{Url}/classrooms/{scenario.Classroom.Id}/summary/export?month={Month}");

        Assert.Equal(1, summary.SessionCount);
        var present = summary.Students.Single(s => s.StudentId == scenario.Students[0].Id);
        Assert.Equal(new AttendanceCountsDto(1, 0, 0, 0, 1, 100), present.Counts);
        Assert.Equal(1, summary.Students.Single(s => s.StudentId == scenario.Students[1].Id).Counts.Absent);

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType?.MediaType);
        using var book = new XLWorkbook(await export.Content.ReadAsStreamAsync());
        var sheet = book.Worksheet("Attendance");
        Assert.Equal("NISN", sheet.Cell(4, 1).GetString());
        Assert.Equal(scenario.Students[0].Nisn, sheet.CellsUsed(c => c.GetString() == scenario.Students[0].Nisn).First().GetString());
    }

    [Fact]
    public async Task A_teacher_sees_summaries_only_of_classrooms_they_work_in()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 0);
        using var demoTeacher = fixture.AsTeacher(); // homeroom teacher of Class A, no lessons in this classroom
        using var principal = fixture.AsPrincipal();

        var foreign = await demoTeacher.GetAsync($"{Url}/classrooms/{scenario.Classroom.Id}/summary?month={Month}");
        var all = await principal.GetAsync($"{Url}/classrooms/{scenario.Classroom.Id}/summary?month={Month}");
        var badMonth = await principal.GetAsync($"{Url}/classrooms/{scenario.Classroom.Id}/summary?month=October");

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badMonth.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    public async Task Students_and_parents_do_not_see_classroom_summaries(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/classrooms/{Guid.NewGuid()}/summary?month=2026-10")).StatusCode);
    }

    [Fact]
    public async Task A_student_sees_their_own_month_lesson_by_lesson()
    {
        var (scenario, session) = await OpenAsync(students: 1);
        using var student = scenario.StudentClient(0);
        (await student.PostJsonAsync($"{Url}/scans", new ScanQrCommand(session.Qr!.Token))).EnsureSuccessStatusCode();

        var mine = await (await student.GetAsync($"{Url}/students/me?month={Month}")).ReadAsync<StudentAttendanceDto>();

        var entry = Assert.Single(mine.Entries);
        Assert.Equal(scenario.Subject.Name, entry.SubjectName);
        Assert.Equal(AttendanceStatus.Present, entry.Status);
        Assert.Equal(1, mine.Counts.Present);
        Assert.Equal(scenario.Classroom.Name, mine.ClassroomName);
    }

    [Fact]
    public async Task A_parent_sees_their_child_and_nobody_else()
    {
        using var parent = fixture.AsParent(); // parent of Rina Putri and the next student of Class A
        using var admin = fixture.AsAdmin();
        using var student = fixture.AsStudent();
        var rina = (await (await student.GetAsync($"{Url}/students/me?month={Month}")).ReadAsync<StudentAttendanceDto>()).StudentId;
        var stranger = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin, "Class B")).Id);

        var child = await parent.GetAsync($"{Url}/students/{rina}?month={Month}");
        var notMine = await parent.GetAsync($"{Url}/students/{stranger.Id}?month={Month}");
        var otherStudent = await student.GetAsync($"{Url}/students/{stranger.Id}?month={Month}");

        Assert.Equal(HttpStatusCode.OK, child.StatusCode);
        Assert.Equal("Rina Putri", (await child.ReadAsync<StudentAttendanceDto>()).FullName);
        Assert.Equal(HttpStatusCode.Forbidden, notMine.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherStudent.StatusCode);
    }

    [Fact]
    public async Task The_scan_page_learns_whether_the_location_is_needed()
    {
        using var student = fixture.AsStudent();

        var settings = await (await student.GetAsync($"{Url}/scan-settings")).ReadAsync<ScanSettingsDto>();

        Assert.False(settings.GeofenceEnabled);
    }
}
