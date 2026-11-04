using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.AchievementDescriptions.AcceptAchievementDescription;
using SchoMans.Application.Features.AchievementDescriptions.DraftAchievementDescription;
using SchoMans.Application.Features.AchievementDescriptions.DraftAchievementDescriptions;
using SchoMans.Application.Features.Grading.EnterGrade;
using SchoMans.Application.Features.Grading.GetGradeBook;
using SchoMans.Application.Features.ReportCards;
using SchoMans.Application.Features.ReportCards.WriteAchievementDescription;
using SchoMans.Domain.Grading;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>"Each AI feature's test set passes" (AI-02): the AI drafts the report-card texts, the teacher accepts, edits or asks again.</summary>
[Collection(ApiCollection.Name)]
public class AchievementDescriptionEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/report-cards/descriptions";

    private async Task<(ClassroomScenario Scenario, Guid TermId)> ScoredClassAsync(int students, Func<int, decimal>? finalScore = null)
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, students, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        for (var i = 0; i < students; i++)
        {
            (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, i, GradeComponent.Final, finalScore?.Invoke(i) ?? 80 + i)).EnsureSuccessStatusCode();
        }

        return (scenario, termId);
    }

    private static async Task<GradeBookRowDto> RowAsync(HttpClient teacher, ClassroomScenario scenario, Guid termId, int student)
    {
        var book = await (await teacher.GetAsync(
            $"/api/v1/grades/book?classroomId={scenario.Classroom.Id}&subjectId={scenario.Subject.Id}&termId={termId}")).ReadAsync<GradeBookDto>();
        return book.Rows.Single(r => r.StudentId == scenario.Students[student].Id);
    }

    private static Task<HttpResponseMessage> DraftAsync(HttpClient teacher, ClassroomScenario scenario, Guid termId, int student) =>
        teacher.PostJsonAsync($"{Url}/draft", new DraftAchievementDescriptionCommand(scenario.Students[student].Id, scenario.Subject.Id, termId));

    // ---- one student ----

    [Fact]
    public async Task The_teacher_gets_a_draft_with_the_real_name_that_waits_for_review()
    {
        var (scenario, termId) = await ScoredClassAsync(1);
        using var teacher = scenario.TeacherClient();
        await LearningObjectiveEndpointTests.CreateAsync(teacher, scenario.Subject.Id, description: "Understands fractions");

        var response = await DraftAsync(teacher, scenario, termId, 0);
        var draft = await response.ReadAsync<AchievementDescriptionDto>();
        var row = await RowAsync(teacher, scenario, termId, 0);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AchievementStatus.Draft, draft.Status);
        Assert.True(draft.IsAiDrafted);
        Assert.Contains(scenario.Students[0].FullName, draft.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("S1", draft.Body, StringComparison.Ordinal);
        Assert.Equal(draft.Body, row.Description);
        Assert.Equal((AchievementStatus.Draft, true), (row.DescriptionStatus, row.DescriptionIsAiDrafted));
    }

    [Fact]
    public async Task Asking_again_replaces_the_draft()
    {
        var (scenario, termId) = await ScoredClassAsync(1);
        using var teacher = scenario.TeacherClient();
        await DraftAsync(teacher, scenario, termId, 0);
        await teacher.PostJsonAsync($"{Url}/accept", new AcceptAchievementDescriptionCommand(scenario.Students[0].Id, scenario.Subject.Id, termId));

        var again = await DraftAsync(teacher, scenario, termId, 0);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var row = await RowAsync(teacher, scenario, termId, 0);
        Assert.Equal(AchievementStatus.Draft, row.DescriptionStatus); // a new draft has to be reviewed again
    }

    [Fact]
    public async Task A_student_without_scores_in_the_subject_gets_no_draft()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();

        var response = await DraftAsync(teacher, scenario, termId, 0);

        Assert.Equal((HttpStatusCode.Conflict, "achievement.no_data"), (response.StatusCode, await response.ProblemCodeAsync()));
    }

    [Fact]
    public async Task An_unknown_student_is_not_found()
    {
        var (scenario, termId) = await ScoredClassAsync(1);
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostJsonAsync($"{Url}/draft", new DraftAchievementDescriptionCommand(Guid.NewGuid(), scenario.Subject.Id, termId));

        Assert.Equal((HttpStatusCode.NotFound, "grades.not_found"), (response.StatusCode, await response.ProblemCodeAsync()));
    }

    // ---- accept, edit, and what families see ----

    [Fact]
    public async Task A_draft_reaches_the_report_card_only_once_the_teacher_accepts_it()
    {
        var (scenario, termId) = await ScoredClassAsync(1);
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(0);
        var studentId = scenario.Students[0].Id;
        var gradesUrl = $"/api/v1/report-cards/grades?studentId={studentId}&termId={termId}";
        var draft = await (await DraftAsync(teacher, scenario, termId, 0)).ReadAsync<AchievementDescriptionDto>();

        // Published with the draft still waiting: the family sees the grades but no text.
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();
        var unreviewed = await (await student.GetAsync(gradesUrl)).ReadAsync<StudentGradesDto>();
        (await teacher.DeleteAsync($"/api/v1/grades/publications?classroomId={scenario.Classroom.Id}&termId={termId}")).EnsureSuccessStatusCode();

        var accepted = await teacher.PostJsonAsync($"{Url}/accept", new AcceptAchievementDescriptionCommand(studentId, scenario.Subject.Id, termId));
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();
        var reviewed = await (await student.GetAsync(gradesUrl)).ReadAsync<StudentGradesDto>();

        Assert.Null(Assert.Single(unreviewed.Subjects).Description);
        var result = await accepted.ReadAsync<AchievementDescriptionDto>();
        Assert.Equal((AchievementStatus.Accepted, true), (result.Status, result.IsAiDrafted));
        Assert.Equal(draft.Body, Assert.Single(reviewed.Subjects).Description);
    }

    [Fact]
    public async Task Editing_a_draft_makes_it_the_teachers_own_accepted_text()
    {
        var (scenario, termId) = await ScoredClassAsync(1);
        using var teacher = scenario.TeacherClient();
        await DraftAsync(teacher, scenario, termId, 0);

        var edit = await teacher.PutJsonAsync("/api/v1/report-cards/descriptions", new WriteAchievementDescriptionCommand(
            scenario.Students[0].Id, scenario.Subject.Id, termId, "My own words about fractions."));
        var row = await RowAsync(teacher, scenario, termId, 0);

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("My own words about fractions.", row.Description);
        Assert.Equal((AchievementStatus.Accepted, false), (row.DescriptionStatus, row.DescriptionIsAiDrafted));
    }

    [Fact]
    public async Task There_is_nothing_to_accept_before_a_description_exists()
    {
        var (scenario, termId) = await ScoredClassAsync(1);
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostJsonAsync($"{Url}/accept", new AcceptAchievementDescriptionCommand(scenario.Students[0].Id, scenario.Subject.Id, termId));

        Assert.Equal((HttpStatusCode.NotFound, "achievement.not_found"), (response.StatusCode, await response.ProblemCodeAsync()));
    }

    [Fact]
    public async Task Published_grades_lock_drafting_and_accepting()
    {
        var (scenario, termId) = await ScoredClassAsync(2);
        using var teacher = scenario.TeacherClient();
        await DraftAsync(teacher, scenario, termId, 0);
        (await GradeEndpointTests.PublishAsync(teacher, scenario, termId)).EnsureSuccessStatusCode();

        var draft = await DraftAsync(teacher, scenario, termId, 1);
        var classroom = await teacher.PostJsonAsync($"{Url}/draft-classroom", new DraftAchievementDescriptionsCommand(scenario.Classroom.Id, scenario.Subject.Id, termId));
        var accept = await teacher.PostJsonAsync($"{Url}/accept", new AcceptAchievementDescriptionCommand(scenario.Students[0].Id, scenario.Subject.Id, termId));

        foreach (var response in new[] { draft, classroom, accept })
        {
            Assert.Equal((HttpStatusCode.Conflict, "grades.published"), (response.StatusCode, await response.ProblemCodeAsync()));
        }
    }

    // ---- a whole classroom ----

    [Fact]
    public async Task A_bulk_run_drafts_every_student_with_scores_and_the_texts_are_not_the_same()
    {
        // The same final score for all three: the AI is told the same facts, so the texts would repeat without the check.
        var (scenario, termId) = await ScoredClassAsync(3, _ => 80);
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostJsonAsync($"{Url}/draft-classroom", new DraftAchievementDescriptionsCommand(scenario.Classroom.Id, scenario.Subject.Id, termId));
        var result = await response.ReadAsync<DraftedDescriptionsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new DraftedDescriptionsDto(3, 0, 0, 0, null), result);
        var texts = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var row = await RowAsync(teacher, scenario, termId, i);
            Assert.Equal(AchievementStatus.Draft, row.DescriptionStatus);
            texts.Add(row.Description!.Replace(scenario.Students[i].FullName, "{name}", StringComparison.Ordinal));
        }

        Assert.Equal(3, texts.Distinct().Count());
    }

    [Fact]
    public async Task A_bulk_run_uses_one_of_the_teachers_daily_calls_and_leaves_existing_texts_alone()
    {
        var (scenario, termId) = await ScoredClassAsync(3);
        using var teacher = scenario.TeacherClient();
        (await teacher.PutJsonAsync("/api/v1/report-cards/descriptions", new WriteAchievementDescriptionCommand(
            scenario.Students[1].Id, scenario.Subject.Id, termId, "Written by the teacher."))).EnsureSuccessStatusCode();

        var result = await (await teacher.PostJsonAsync($"{Url}/draft-classroom", new DraftAchievementDescriptionsCommand(scenario.Classroom.Id, scenario.Subject.Id, termId)))
            .ReadAsync<DraftedDescriptionsDto>();
        var again = await (await teacher.PostJsonAsync($"{Url}/draft-classroom", new DraftAchievementDescriptionsCommand(scenario.Classroom.Id, scenario.Subject.Id, termId)))
            .ReadAsync<DraftedDescriptionsDto>();

        Assert.Equal(new DraftedDescriptionsDto(2, 1, 0, 0, null), result);
        Assert.Equal(new DraftedDescriptionsDto(0, 3, 0, 0, null), again);
        Assert.Equal("Written by the teacher.", (await RowAsync(teacher, scenario, termId, 1)).Description);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teacherUserId = await db.Users.Where(u => u.Email == scenario.Teacher.Email).Select(u => u.Id).SingleAsync();
        var calls = await db.AiUsageLogs.Where(l => l.UserId == teacherUserId && l.Feature == "AI-02").ToListAsync();
        Assert.Equal(2, calls.Count);
        Assert.Equal(1, calls.Count(l => !l.IsBulk));
    }

    [Fact]
    public async Task A_bulk_run_skips_students_without_scores_and_reports_it()
    {
        var withoutScores = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = withoutScores.TeacherClient();

        var result = await (await teacher.PostJsonAsync($"{Url}/draft-classroom", new DraftAchievementDescriptionsCommand(
            withoutScores.Classroom.Id, withoutScores.Subject.Id, termId))).ReadAsync<DraftedDescriptionsDto>();

        Assert.Equal(new DraftedDescriptionsDto(0, 0, 1, 0, null), result);
    }

    // ---- who may do it ----

    [Fact]
    public async Task Only_the_teacher_of_the_subject_in_the_classroom_can_draft_and_accept()
    {
        var (scenario, termId) = await ScoredClassAsync(1);
        var studentId = scenario.Students[0].Id;
        object[] bodies =
        [
            new DraftAchievementDescriptionCommand(studentId, scenario.Subject.Id, termId),
            new DraftAchievementDescriptionsCommand(scenario.Classroom.Id, scenario.Subject.Id, termId),
            new AcceptAchievementDescriptionCommand(studentId, scenario.Subject.Id, termId),
        ];
        string[] urls = [$"{Url}/draft", $"{Url}/draft-classroom", $"{Url}/accept"];
        var clients = new[] { fixture.AsStudent(), fixture.AsParent(), fixture.AsAdmin(), fixture.AsPrincipal() };

        try
        {
            foreach (var client in clients)
            {
                for (var i = 0; i < urls.Length; i++)
                {
                    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(urls[i], bodies[i])).StatusCode);
                }
            }

            using var otherTeacher = (await ClassroomScenario.CreateAsync(fixture.Factory, 1)).TeacherClient();
            for (var i = 0; i < urls.Length; i++)
            {
                var response = await otherTeacher.PostJsonAsync(urls[i], bodies[i]);
                Assert.Equal((HttpStatusCode.Forbidden, "grades.not_your_class"), (response.StatusCode, await response.ProblemCodeAsync()));
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
    }
}
