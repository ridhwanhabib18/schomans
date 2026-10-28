using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.ReportCards;
using SchoMans.Application.Features.ReportCards.GenerateReportCards;
using SchoMans.Application.Features.ReportCards.WriteAchievementDescription;
using SchoMans.Domain.Grading;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>"Report-card PDFs generate": families see published grades, the homeroom teacher generates the PDFs.</summary>
[Collection(ApiCollection.Name)]
public class ReportCardEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/report-cards";

    private static string GradesUrl(Guid studentId, Guid termId) => $"{Url}/grades?studentId={studentId}&termId={termId}";

    private static string PdfUrl(Guid studentId, Guid termId) => $"{Url}/url?studentId={studentId}&termId={termId}";

    private async Task<(ClassroomScenario Scenario, Guid TermId)> GradedClassAsync(int students = 2)
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, students, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        for (var i = 0; i < students; i++)
        {
            (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, i, GradeComponent.Final, 80 + i)).EnsureSuccessStatusCode();
        }

        return (scenario, termId);
    }

    [Fact]
    public async Task A_student_sees_their_grades_and_descriptions_only_after_they_are_published()
    {
        var (scenario, termId) = await GradedClassAsync();
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(0);
        var described = await teacher.PutJsonAsync($"{Url}/descriptions", new WriteAchievementDescriptionCommand(
            scenario.Students[0].Id, scenario.Subject.Id, termId, "Masters fractions."));

        var before = await student.GetAsync(GradesUrl(scenario.Students[0].Id, termId));
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();
        var after = await (await student.GetAsync(GradesUrl(scenario.Students[0].Id, termId))).ReadAsync<StudentGradesDto>();

        Assert.Equal(HttpStatusCode.OK, described.StatusCode);
        Assert.Equal((HttpStatusCode.NotFound, "grades.not_published"), (before.StatusCode, await before.ProblemCodeAsync()));
        var subject = Assert.Single(after.Subjects);
        Assert.Equal(scenario.Subject.Name, subject.SubjectName);
        Assert.Equal(80m, subject.FinalGrade);
        Assert.Equal("B", subject.Band);
        Assert.Equal("Masters fractions.", subject.Description);
        Assert.False(after.HasReportCard);
    }

    [Fact]
    public async Task A_parent_sees_the_published_grades_of_their_child_only()
    {
        var (scenario, termId) = await GradedClassAsync();
        using var teacher = scenario.TeacherClient();
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();
        var email = TestData.NextEmail("parent");
        using var parent = fixture.AsUser(email, "Test Parent", $"sub-{email}", "Parent");
        (await parent.GetAsync("/api/v1/dashboard")).EnsureSuccessStatusCode(); // the first request creates the account
        await LinkParentAsync(email, scenario.Students[0].Id);

        var own = await parent.GetAsync(GradesUrl(scenario.Students[0].Id, termId));
        var other = await parent.GetAsync(GradesUrl(scenario.Students[1].Id, termId));
        using var classmate = scenario.StudentClient(1);
        var notMine = await classmate.GetAsync(GradesUrl(scenario.Students[0].Id, termId));

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal("grades.not_yours", await notMine.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Principal")]
    [InlineData("Admin")]
    public async Task Staff_use_the_grade_book_instead_of_the_family_view(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.GetAsync(GradesUrl(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_homeroom_teacher_generates_a_pdf_per_student_once_grades_are_published()
    {
        var (scenario, termId) = await GradedClassAsync();
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(0);
        using var principal = fixture.AsPrincipal();
        var command = new GenerateReportCardsCommand(scenario.Classroom.Id, termId);

        var tooEarly = await teacher.PostJsonAsync($"{Url}/generate", command);
        var studentTooEarly = await student.GetAsync(PdfUrl(scenario.Students[0].Id, termId));
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();
        var generated = await (await teacher.PostJsonAsync($"{Url}/generate", command)).ReadAsync<GeneratedReportCardsDto>();
        var studentUrl = await student.GetAsync(PdfUrl(scenario.Students[0].Id, termId));
        var grades = await (await student.GetAsync(GradesUrl(scenario.Students[0].Id, termId))).ReadAsync<StudentGradesDto>();
        var classmatePdf = await student.GetAsync(PdfUrl(scenario.Students[1].Id, termId));
        var principalUrl = await principal.GetAsync(PdfUrl(scenario.Students[1].Id, termId));

        Assert.Equal((HttpStatusCode.Conflict, "report_cards.grades_not_published"), (tooEarly.StatusCode, await tooEarly.ProblemCodeAsync()));
        Assert.Equal("report_cards.not_found", await studentTooEarly.ProblemCodeAsync());
        Assert.Equal(2, generated.Count);
        foreach (var pupil in scenario.Students)
        {
            var info = await fixture.Storage.GetObjectInfoAsync(FileBucket.Private, $"report-cards/{termId:N}/{pupil.Id:N}.pdf", CancellationToken.None);
            Assert.NotNull(info);
            Assert.Equal("application/pdf", info.ContentType);
            Assert.True(info.SizeBytes > 1000);
        }

        Assert.Contains($"report-cards/{termId:N}/{scenario.Students[0].Id:N}.pdf", (await studentUrl.ReadAsync<ReportCardUrlDto>()).Url);
        Assert.True(grades.HasReportCard);
        Assert.Equal("report_cards.not_yours", await classmatePdf.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, principalUrl.StatusCode);
    }

    [Fact]
    public async Task Unpublishing_the_grades_withdraws_the_report_cards_from_the_family()
    {
        var (scenario, termId) = await GradedClassAsync(1);
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(0);
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();
        (await teacher.PostJsonAsync($"{Url}/generate", new GenerateReportCardsCommand(scenario.Classroom.Id, termId))).EnsureSuccessStatusCode();

        (await teacher.DeleteAsync($"/api/v1/grades/publications?classroomId={scenario.Classroom.Id}&termId={termId}")).EnsureSuccessStatusCode();
        var studentUrl = await student.GetAsync(PdfUrl(scenario.Students[0].Id, termId));
        var teacherUrl = await teacher.GetAsync(PdfUrl(scenario.Students[0].Id, termId));

        Assert.Equal(HttpStatusCode.Forbidden, studentUrl.StatusCode);
        Assert.Equal(HttpStatusCode.OK, teacherUrl.StatusCode);
    }

    [Fact]
    public async Task Only_the_homeroom_teacher_generates_and_only_a_teacher_of_the_subject_describes()
    {
        var (scenario, termId) = await GradedClassAsync(1);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: DayOfWeek.Sunday);
        using var stranger = other.TeacherClient();
        using var teacher = scenario.TeacherClient();

        var describe = await stranger.PutJsonAsync($"{Url}/descriptions", new WriteAchievementDescriptionCommand(scenario.Students[0].Id, scenario.Subject.Id, termId, "Text"));
        var empty = await teacher.PutJsonAsync($"{Url}/descriptions", new WriteAchievementDescriptionCommand(scenario.Students[0].Id, scenario.Subject.Id, termId, ""));
        var generate = await stranger.PostJsonAsync($"{Url}/generate", new GenerateReportCardsCommand(scenario.Classroom.Id, termId));
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();
        var locked = await teacher.PutJsonAsync($"{Url}/descriptions", new WriteAchievementDescriptionCommand(scenario.Students[0].Id, scenario.Subject.Id, termId, "Late text"));

        Assert.Equal("grades.not_your_class", await describe.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("grades.not_homeroom", await generate.ProblemCodeAsync());
        Assert.Equal((HttpStatusCode.Conflict, "grades.published"), (locked.StatusCode, await locked.ProblemCodeAsync()));
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Admin")]
    [InlineData("Principal")]
    public async Task Only_teachers_describe_and_generate(string role)
    {
        using var client = fixture.AsNewUser(role);

        var describe = await client.PutJsonAsync($"{Url}/descriptions", new WriteAchievementDescriptionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Text"));
        var generate = await client.PostJsonAsync($"{Url}/generate", new GenerateReportCardsCommand(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, describe.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, generate.StatusCode);
    }

    private async Task LinkParentAsync(string parentEmail, Guid studentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parentId = await db.Users.Where(u => u.Email == parentEmail).Select(u => u.Id).SingleAsync();
        db.StudentParents.Add(StudentParent.Create(parentId, studentId));
        await db.SaveChangesAsync();
    }
}
