using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Attendance;
using SchoMans.Application.Features.Attendance.MarkAttendance;
using SchoMans.Application.Features.Attendance.OpenSession;
using SchoMans.Application.Features.Audit.ListAuditLogs;
using SchoMans.Application.Features.Requests;
using SchoMans.Application.Features.Requests.CreateRequest;
using SchoMans.Application.Features.Requests.DecideRequest;
using SchoMans.Application.Features.Students.ListMyChildren;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Grading;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>F-11 audit log: every change to a grade, an attendance record or an approval is recorded with who, when, and the old and new values.</summary>
[Collection(ApiCollection.Name)]
public class AuditEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/audit-logs";

    private static JsonElement Values(string? json) => JsonDocument.Parse(json ?? "{}").RootElement;

    private async Task<List<AuditLogDto>> TrailAsync(string entityName, Guid entityId)
    {
        using var admin = fixture.AsAdmin();
        var page = await (await admin.GetAsync($"{Url}?entityName={entityName}&entityId={entityId}&pageSize=100")).ReadAsync<PagedResult<AuditLogDto>>();
        return page.Items.OrderBy(l => l.At).ToList();
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    private async Task<Guid> GradeIdAsync(Guid studentId, Guid subjectId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Grades
            .Where(g => g.StudentId == studentId && g.SubjectId == subjectId).Select(g => g.Id).SingleAsync();
    }

    [Fact]
    public async Task Entering_and_changing_a_grade_is_recorded_with_the_teacher_and_the_old_and_new_score()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();

        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Midterm, 70)).EnsureSuccessStatusCode();
        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Midterm, 85)).EnsureSuccessStatusCode();

        var trail = await TrailAsync("Grade", await GradeIdAsync(scenario.Students[0].Id, scenario.Subject.Id));
        var teacherId = await UserIdAsync(scenario.Teacher.Email);
        Assert.Equal(["Created", "Updated"], trail.Select(l => l.Action));
        Assert.All(trail, l => Assert.Equal(teacherId, l.UserId));
        Assert.Equal(scenario.Teacher.FullName, trail[0].UserName);
        Assert.Equal(70m, Values(trail[0].NewValues).GetProperty("Score").GetDecimal());
        Assert.Null(trail[0].OldValues);
        Assert.Equal(70m, Values(trail[1].OldValues).GetProperty("Score").GetDecimal());
        Assert.Equal(85m, Values(trail[1].NewValues).GetProperty("Score").GetDecimal());
    }

    [Fact]
    public async Task Saving_the_same_score_again_leaves_no_trace()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();

        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Quiz, 60)).EnsureSuccessStatusCode();
        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Quiz, 60)).EnsureSuccessStatusCode();

        var trail = await TrailAsync("Grade", await GradeIdAsync(scenario.Students[0].Id, scenario.Subject.Id));
        Assert.Equal(["Created"], trail.Select(l => l.Action));
    }

    [Fact]
    public async Task A_manual_attendance_mark_is_recorded_with_the_old_and_new_status()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var session = await (await teacher.PostJsonAsync("/api/v1/attendance/sessions", new OpenSessionCommand(scenario.Lesson.Id))).ReadAsync<AttendanceSessionDto>();
        var studentId = scenario.Students[0].Id;

        (await teacher.PutJsonAsync($"/api/v1/attendance/sessions/{session.Id}/students/{studentId}", new MarkAttendanceCommand(session.Id, studentId, AttendanceStatus.Present, "No camera"))).EnsureSuccessStatusCode();
        (await teacher.PutJsonAsync($"/api/v1/attendance/sessions/{session.Id}/students/{studentId}", new MarkAttendanceCommand(session.Id, studentId, AttendanceStatus.Sick, "Doctor's note"))).EnsureSuccessStatusCode();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var recordId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceRecords
            .Where(r => r.SessionId == session.Id && r.StudentId == studentId).Select(r => r.Id).SingleAsync();
        var trail = await TrailAsync("AttendanceRecord", recordId);

        var last = trail[^1];
        Assert.Equal("Updated", last.Action);
        Assert.Equal("Present", Values(last.OldValues).GetProperty("Status").GetString());
        Assert.Equal("Sick", Values(last.NewValues).GetProperty("Status").GetString());
        Assert.Equal(await UserIdAsync(scenario.Teacher.Email), last.UserId);
    }

    [Fact]
    public async Task A_decision_on_a_request_is_recorded_for_the_approval_and_the_request()
    {
        using var parent = fixture.AsParent();
        using var teacher = fixture.AsTeacher();
        using var principal = fixture.AsPrincipal();
        var children = await (await parent.GetAsync("/api/v1/users/me/children")).ReadAsync<List<ChildDto>>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var created = await (await parent.PostJsonAsync("/api/v1/requests", new CreateRequestCommand(
            RequestType.StudentLeave, children.Single(c => c.FullName == "Rina Putri").StudentId, today.AddDays(1), today.AddDays(2), "Fever"))).ReadAsync<RequestDetailDto>();

        (await teacher.PostJsonAsync($"/api/v1/requests/{created.Id}/decision", new DecideRequestCommand(created.Id, true, "Get well soon"))).EnsureSuccessStatusCode();
        (await principal.PostJsonAsync($"/api/v1/requests/{created.Id}/decision", new DecideRequestCommand(created.Id, true, null))).EnsureSuccessStatusCode();

        var requestTrail = await TrailAsync("Request", created.Id);
        Assert.Equal("Created", requestTrail[0].Action);
        Assert.Equal("Approved", Values(requestTrail[^1].NewValues).GetProperty("Status").GetString());
        Assert.Equal("Pending", Values(requestTrail[^1].OldValues).GetProperty("Status").GetString());

        using var admin = fixture.AsAdmin();
        var approvals = await (await admin.GetAsync($"{Url}?entityName=Approval&pageSize=100")).ReadAsync<PagedResult<AuditLogDto>>();
        var decided = approvals.Items.Where(l => l.Action == "Created" && Values(l.NewValues).GetProperty("RequestId").GetGuid() == created.Id).ToList();
        Assert.Equal(2, decided.Count);
        Assert.Contains(decided, l => l.UserName == "Budi Santoso");
        Assert.Contains(decided, l => l.UserName == "Hendra Wirawan");
    }

    [Fact]
    public async Task The_trail_can_be_filtered_by_entity_user_and_day()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Quiz, 55)).EnsureSuccessStatusCode();
        var teacherId = await UserIdAsync(scenario.Teacher.Email);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        using var principal = fixture.AsPrincipal();
        var byUser = await (await principal.GetAsync($"{Url}?userId={teacherId}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&pageSize=100")).ReadAsync<PagedResult<AuditLogDto>>();
        Assert.NotEmpty(byUser.Items);
        Assert.All(byUser.Items, l => Assert.Equal(teacherId, l.UserId));

        var otherDay = await (await principal.GetAsync($"{Url}?userId={teacherId}&to={today.AddDays(-2):yyyy-MM-dd}&pageSize=100")).ReadAsync<PagedResult<AuditLogDto>>();
        Assert.Empty(otherDay.Items);
    }

    [Fact]
    public async Task The_newest_change_comes_first_and_paging_works()
    {
        using var admin = fixture.AsAdmin();
        var first = await (await admin.GetAsync($"{Url}?pageSize=2")).ReadAsync<PagedResult<AuditLogDto>>();

        Assert.Equal((1, 2), (first.Page, first.PageSize));
        Assert.True(first.Items.Count <= 2);
        Assert.Equal(first.Items.OrderByDescending(l => l.At).Select(l => l.Id), first.Items.Select(l => l.Id));
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("from=2026-05-02&to=2026-05-01")]
    public async Task Invalid_filters_are_validation_errors(string query)
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.GetAsync($"{Url}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Applicant")]
    public async Task Only_admin_staff_and_the_principal_may_read_the_trail(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task The_principal_may_read_the_trail_and_an_anonymous_caller_may_not()
    {
        using var principal = fixture.AsPrincipal();
        Assert.Equal(HttpStatusCode.OK, (await principal.GetAsync(Url)).StatusCode);

        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url)).StatusCode);
    }
}
