using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Grading.EnterGrade;
using SchoMans.Application.Features.Grading.GetGradeBook;
using SchoMans.Application.Features.LearningObjectives;
using SchoMans.Application.Features.LearningObjectives.CreateLearningObjective;
using SchoMans.Application.Features.LearningObjectives.UpdateLearningObjective;
using SchoMans.Domain.Grading;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>Learning objectives (AI-02): kept by the teachers of a subject, and scores can be linked to them.</summary>
[Collection(ApiCollection.Name)]
public class LearningObjectiveEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/learning-objectives";

    private static int _counter;

    /// <summary>A code nobody else uses: the subjects are shared by the tests and the demo data already has objectives with simple codes.</summary>
    internal static string NewCode() => $"T{Interlocked.Increment(ref _counter)}-{Guid.NewGuid().ToString("N")[..6]}";

    internal static async Task<LearningObjectiveDto> CreateAsync(HttpClient teacher, Guid subjectId, string? code = null, string description = "Understands fractions")
    {
        var response = await teacher.PostJsonAsync(Url, new CreateLearningObjectiveCommand(subjectId, code ?? NewCode(), description));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<LearningObjectiveDto>();
    }

    [Fact]
    public async Task A_teacher_of_the_subject_adds_lists_edits_and_removes_objectives()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var second = await CreateAsync(teacher, scenario.Subject.Id, "ZZ-" + NewCode(), "Reads graphs");
        var first = await CreateAsync(teacher, scenario.Subject.Id, "ZY-" + NewCode());
        var listed = await (await teacher.GetAsync($"{Url}?subjectId={scenario.Subject.Id}")).ReadAsync<List<LearningObjectiveDto>>();
        var edited = await teacher.PutJsonAsync($"{Url}/{first.Id}", new UpdateLearningObjectiveCommand(first.Id, first.Code, "Understands fractions and decimals"));
        var removed = await teacher.DeleteAsync($"{Url}/{second.Id}");
        var after = await (await teacher.GetAsync($"{Url}?subjectId={scenario.Subject.Id}")).ReadAsync<List<LearningObjectiveDto>>();

        Assert.Equal([first.Id, second.Id], listed.Where(o => o.Id == first.Id || o.Id == second.Id).Select(o => o.Id)); // by code
        Assert.Equal("Understands fractions and decimals", (await edited.ReadAsync<LearningObjectiveDto>()).Description);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Contains(after, o => o.Id == first.Id);
        Assert.DoesNotContain(after, o => o.Id == second.Id);
    }

    [Fact]
    public async Task A_code_is_used_once_per_subject()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var existing = await CreateAsync(teacher, scenario.Subject.Id);

        var duplicate = await teacher.PostJsonAsync(Url, new CreateLearningObjectiveCommand(scenario.Subject.Id, $" {existing.Code} ", "Another text"));

        Assert.Equal((HttpStatusCode.Conflict, "objectives.code_taken"), (duplicate.StatusCode, await duplicate.ProblemCodeAsync()));
    }

    [Fact]
    public async Task An_objective_needs_a_code_and_a_description_that_are_not_too_long()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var empty = await teacher.PostJsonAsync(Url, new CreateLearningObjectiveCommand(scenario.Subject.Id, "", "Text"));
        var tooLong = await teacher.PostJsonAsync(Url, new CreateLearningObjectiveCommand(scenario.Subject.Id, NewCode(), new string('x', LearningObjective.DescriptionMaxLength + 1)));
        var missingSubject = await teacher.PostJsonAsync(Url, new CreateLearningObjectiveCommand(Guid.NewGuid(), NewCode(), "Text"));

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal((HttpStatusCode.NotFound, "objectives.subject_not_found"), (missingSubject.StatusCode, await missingSubject.ProblemCodeAsync()));
    }

    [Fact]
    public async Task Only_the_teachers_of_the_subject_manage_its_objectives()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var owner = scenario.TeacherClient();
        var objective = await CreateAsync(owner, scenario.Subject.Id);
        var clients = new[] { fixture.AsStudent(), fixture.AsParent(), fixture.AsAdmin(), fixture.AsPrincipal() };

        try
        {
            foreach (var client in clients)
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}?subjectId={scenario.Subject.Id}")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, new CreateLearningObjectiveCommand(scenario.Subject.Id, NewCode(), "Text"))).StatusCode);
            }

            // A teacher who teaches another subject only.
            using var other = (await ClassroomScenario.CreateAsync(fixture.Factory, 1, subjectName: "English")).TeacherClient();
            foreach (var response in new[]
            {
                await other.GetAsync($"{Url}?subjectId={scenario.Subject.Id}"),
                await other.PutJsonAsync($"{Url}/{objective.Id}", new UpdateLearningObjectiveCommand(objective.Id, NewCode(), "Changed")),
                await other.DeleteAsync($"{Url}/{objective.Id}"),
            })
            {
                Assert.Equal((HttpStatusCode.Forbidden, "objectives.not_your_subject"), (response.StatusCode, await response.ProblemCodeAsync()));
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

    [Fact]
    public async Task A_score_can_be_linked_to_an_objective_and_the_grade_book_can_show_those_scores_alone()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        var objective = await CreateAsync(teacher, scenario.Subject.Id);
        var studentId = scenario.Students[0].Id;

        (await GradeEndpointTests.EnterAsync(teacher, scenario, termId, 0, GradeComponent.Quiz, 60)).EnsureSuccessStatusCode();
        var linked = await teacher.PutJsonAsync("/api/v1/grades", new EnterGradeCommand(studentId, scenario.Subject.Id, termId, GradeComponent.Quiz, 90, objective.Id));
        await teacher.PutJsonAsync("/api/v1/grades", new EnterGradeCommand(studentId, scenario.Subject.Id, termId, GradeComponent.Quiz, 100, objective.Id)); // a correction
        var book = $"/api/v1/grades/book?classroomId={scenario.Classroom.Id}&subjectId={scenario.Subject.Id}&termId={termId}";
        var overall = (await (await teacher.GetAsync(book)).ReadAsync<GradeBookDto>()).Rows.Single();
        var forObjective = (await (await teacher.GetAsync($"{book}&learningObjectiveId={objective.Id}")).ReadAsync<GradeBookDto>()).Rows.Single();

        Assert.Equal(objective.Id, (await linked.ReadAsync<EnteredGradeDto>()).LearningObjectiveId);
        var overallQuiz = overall.Components.Single(c => c.Component == GradeComponent.Quiz);
        Assert.Equal((2, 80m), (overallQuiz.Count, overallQuiz.Average)); // the plain score and the linked one
        var objectiveQuiz = forObjective.Components.Single(c => c.Component == GradeComponent.Quiz);
        Assert.Equal((1, 100m), (objectiveQuiz.Count, objectiveQuiz.Average));
        Assert.Equal(overall.FinalGrade, forObjective.FinalGrade); // the final grade always counts every score
    }

    [Fact]
    public async Task A_score_cannot_be_linked_to_an_objective_of_another_subject()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday, subjectName: "English");
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        using var otherTeacher = other.TeacherClient();
        var foreign = await CreateAsync(otherTeacher, other.Subject.Id);

        var response = await teacher.PutJsonAsync("/api/v1/grades", new EnterGradeCommand(scenario.Students[0].Id, scenario.Subject.Id, termId, GradeComponent.Quiz, 90, foreign.Id));

        Assert.Equal((HttpStatusCode.NotFound, "grades.objective_not_found"), (response.StatusCode, await response.ProblemCodeAsync()));
    }

    [Fact]
    public async Task An_objective_with_linked_scores_cannot_be_removed()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await GradeEndpointTests.ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        var objective = await CreateAsync(teacher, scenario.Subject.Id);
        (await teacher.PutJsonAsync("/api/v1/grades", new EnterGradeCommand(scenario.Students[0].Id, scenario.Subject.Id, termId, GradeComponent.Quiz, 90, objective.Id))).EnsureSuccessStatusCode();

        var response = await teacher.DeleteAsync($"{Url}/{objective.Id}");

        Assert.Equal((HttpStatusCode.Conflict, "objectives.in_use"), (response.StatusCode, await response.ProblemCodeAsync()));
    }
}
