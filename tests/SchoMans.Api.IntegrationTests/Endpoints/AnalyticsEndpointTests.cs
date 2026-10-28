using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Dashboard.GetAnalytics;
using SchoMans.Domain.Grading;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>"Charts show per role": every role gets the analytics of its own dashboard (F-09).</summary>
[Collection(ApiCollection.Name)]
public class AnalyticsEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/dashboard/analytics";

    [Fact]
    public async Task The_principal_sees_the_attendance_trend_per_classroom_and_the_average_per_subject()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using (var teacher = scenario.TeacherClient())
        {
            (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Final, 77)).EnsureSuccessStatusCode();
        }

        using var principal = fixture.AsPrincipal();
        var analytics = await (await principal.GetAsync(Url)).ReadAsync<AnalyticsDto>();

        Assert.Equal("Principal", analytics.Role);
        Assert.Null(analytics.Teacher);
        Assert.Empty(analytics.Children);
        var trend = Assert.Single(analytics.Principal!.AttendanceTrend, t => t.ClassroomId == scenario.Classroom.Id);
        Assert.Equal(GetAnalyticsHandler.TrendMonths, trend.Months.Count);
        Assert.All(trend.Months, m => Assert.Null(m.Rate)); // the new classroom has no attendance yet
        Assert.Contains(analytics.Principal.SubjectAverages, s => s.SubjectId == scenario.Subject.Id && s.Students >= 1);
    }

    [Fact]
    public async Task A_teacher_sees_the_students_below_the_minimum_passing_score_of_their_classes()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 2, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Final, 40)).EnsureSuccessStatusCode();
        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 1, GradeComponent.Final, 95)).EnsureSuccessStatusCode();

        var analytics = await (await teacher.GetAsync(Url)).ReadAsync<AnalyticsDto>();

        Assert.Equal(GetAnalyticsHandler.AttendanceThreshold, analytics.Teacher!.AttendanceThreshold);
        var flagged = Assert.Single(analytics.Teacher.Flagged);
        Assert.Equal(scenario.Students[0].Id, flagged.StudentId);
        Assert.Equal([scenario.Subject.Name], flagged.BelowPassingSubjects);
        Assert.False(flagged.LowAttendance);
        Assert.Equal(scenario.Classroom.Name, flagged.ClassroomName);
    }

    [Fact]
    public async Task A_student_sees_their_monthly_grades_only_once_they_are_published()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(0);
        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Quiz, 84)).EnsureSuccessStatusCode();

        var before = await (await student.GetAsync(Url)).ReadAsync<AnalyticsDto>();
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();
        var after = await (await student.GetAsync(Url)).ReadAsync<AnalyticsDto>();

        var child = Assert.Single(before.Children);
        Assert.Equal(scenario.Students[0].Id, child.StudentId);
        Assert.Equal(GetAnalyticsHandler.TrendMonths, child.Months.Count);
        Assert.All(child.Months, m => Assert.Null(m.AverageGrade));
        Assert.Equal(84m, Assert.Single(after.Children).Months[^1].AverageGrade);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Applicant")]
    public async Task Roles_without_analytics_get_an_empty_answer(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.GetAsync(Url);
        var analytics = await response.ReadAsync<AnalyticsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(analytics.Principal);
        Assert.Null(analytics.Teacher);
        Assert.Empty(analytics.Children);
    }

    [Fact]
    public async Task Analytics_need_a_signed_in_user()
    {
        using var client = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url)).StatusCode);
    }
}
