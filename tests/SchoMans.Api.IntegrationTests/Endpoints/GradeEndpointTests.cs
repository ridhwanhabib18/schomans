using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.AcademicYears;
using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.Assignments.CreateAssignment;
using SchoMans.Application.Features.Assignments.GradeSubmission;
using SchoMans.Application.Features.Assignments.SubmitAssignment;
using SchoMans.Application.Features.GradeWeights;
using SchoMans.Application.Features.GradeWeights.SetGradeWeights;
using SchoMans.Application.Features.Grading.EnterGrade;
using SchoMans.Application.Features.Grading.GetGradeBook;
using SchoMans.Application.Features.Grading.PublishGrades;
using SchoMans.Domain.Grading;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>F-05 grading: scores flow in from assignments, teachers enter the other components, the homeroom teacher publishes.</summary>
[Collection(ApiCollection.Name)]
public class GradeEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/grades";

    internal static async Task<Guid> ActiveTermIdAsync(ApiFixture fixture)
    {
        using var admin = fixture.AsAdmin();
        var years = await (await admin.GetAsync("/api/v1/academic-years?pageSize=100")).ReadAsync<PagedResult<AcademicYearDto>>();
        return years.Items.Single(y => y.IsActive).Terms.Single(t => t.IsActive).Id;
    }

    /// <summary>The teacher gives an assignment, the student hands it in and the teacher scores it.</summary>
    internal static async Task<SubmissionDto> ScoredAssignmentAsync(ClassroomScenario scenario, int student, decimal score)
    {
        using var teacher = scenario.TeacherClient();
        using var pupil = scenario.StudentClient(student);
        var assignment = await (await teacher.PostJsonAsync("/api/v1/assignments", new CreateAssignmentCommand(
            "Fractions", null, DateTimeOffset.UtcNow.AddDays(2), scenario.Subject.Id, [scenario.Classroom.Id], null))).ReadAsync<AssignmentDto>();
        var submission = await (await pupil.PutJsonAsync($"/api/v1/assignments/{assignment.Id}/submission", new SubmitAssignmentCommand(assignment.Id, null, null, "My answer"))).ReadAsync<SubmissionDto>();
        var scored = await teacher.PutJsonAsync($"/api/v1/assignments/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, score, null));
        Assert.Equal(HttpStatusCode.OK, scored.StatusCode);
        return submission;
    }

    internal static Task<HttpResponseMessage> EnterAsync(HttpClient teacher, ClassroomScenario scenario, Guid termId, int student, GradeComponent component, decimal score) =>
        teacher.PutJsonAsync(Url, new EnterGradeCommand(scenario.Students[student].Id, scenario.Subject.Id, termId, component, score));

    internal static Task<HttpResponseMessage> PublishAsync(HttpClient teacher, ClassroomScenario scenario, Guid termId) =>
        teacher.PostJsonAsync($"{Url}/publications", new PublishGradesCommand(scenario.Classroom.Id, termId));

    private static string BookUrl(ClassroomScenario scenario, Guid termId) =>
        $"{Url}/book?classroomId={scenario.Classroom.Id}&subjectId={scenario.Subject.Id}&termId={termId}";

    [Fact]
    public async Task A_scored_assignment_and_entered_scores_make_the_final_grade_and_band()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 2, day: DayOfWeek.Saturday);
        var termId = await ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        await ScoredAssignmentAsync(scenario, 0, 90);

        var midterm = await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Midterm, 70);
        await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Midterm, 80); // a correction, not a second score
        var book = await (await teacher.GetAsync(BookUrl(scenario, termId))).ReadAsync<GradeBookDto>();

        Assert.Equal(HttpStatusCode.OK, midterm.StatusCode);
        Assert.Equal(2, book.Rows.Count);
        var row = book.Rows.Single(r => r.StudentId == scenario.Students[0].Id);
        Assert.Equal(90m, row.Components.Single(c => c.Component == GradeComponent.Assignment).Average);
        Assert.Equal(1, row.Components.Single(c => c.Component == GradeComponent.Midterm).Count);
        Assert.Null(row.Components.Single(c => c.Component == GradeComponent.Final).Average);
        // Default weights: assignment 30, midterm 20; quiz and final have no scores: (90*30 + 80*20) / 50 = 86
        Assert.Equal(86m, row.FinalGrade);
        Assert.Equal("B", row.Band);
        Assert.True(row.IsPassing);
        var empty = book.Rows.Single(r => r.StudentId == scenario.Students[1].Id);
        Assert.Null(empty.FinalGrade);
        Assert.Null(empty.Band);
        Assert.False(book.IsPublished);
        Assert.True(book.Weights.IsDefault);
    }

    [Fact]
    public async Task Rescoring_an_assignment_corrects_its_grade_instead_of_adding_one()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        var submission = await ScoredAssignmentAsync(scenario, 0, 40);

        (await teacher.PutJsonAsync($"/api/v1/assignments/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, 60, null))).EnsureSuccessStatusCode();
        var row = Assert.Single((await (await teacher.GetAsync(BookUrl(scenario, termId))).ReadAsync<GradeBookDto>()).Rows);

        var assignment = row.Components.Single(c => c.Component == GradeComponent.Assignment);
        Assert.Equal((60m, 1), (assignment.Average!.Value, assignment.Count));
    }

    [Fact]
    public async Task A_grade_below_the_minimum_passing_score_is_flagged()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();

        await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Final, 20);
        var book = await (await teacher.GetAsync(BookUrl(scenario, termId))).ReadAsync<GradeBookDto>();

        var row = Assert.Single(book.Rows);
        Assert.Equal(20m, row.FinalGrade);
        Assert.Equal("E", row.Band);
        Assert.False(row.IsPassing);
    }

    [Fact]
    public async Task Invalid_grades_are_validation_errors()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();

        var assignment = await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Assignment, 80);
        var tooHigh = await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Final, 101);
        var noQuery = await teacher.GetAsync($"{Url}/book");

        Assert.Equal(HttpStatusCode.BadRequest, assignment.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noQuery.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Admin")]
    [InlineData("Principal")]
    public async Task Only_teachers_enter_and_publish_grades(string role)
    {
        using var client = fixture.AsNewUser(role);

        var enter = await client.PutJsonAsync(Url, new EnterGradeCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), GradeComponent.Quiz, 80));
        var publish = await client.PostJsonAsync($"{Url}/publications", new PublishGradesCommand(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, enter.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Admin")]
    public async Task Families_and_admin_do_not_open_the_grade_book(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.GetAsync($"{Url}/book?classroomId={Guid.NewGuid()}&subjectId={Guid.NewGuid()}&termId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_teacher_only_grades_the_subjects_they_teach_in_the_classroom()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: DayOfWeek.Sunday);
        var termId = await ActiveTermIdAsync(fixture);
        using var stranger = other.TeacherClient();
        using var principal = fixture.AsPrincipal();

        var enter = await EnterAsync(stranger, scenario, termId, 0, GradeComponent.Quiz, 80);
        var book = await stranger.GetAsync(BookUrl(scenario, termId));
        var publish = await PublishAsync(stranger, scenario, termId);
        var principalBook = await principal.GetAsync(BookUrl(scenario, termId));

        Assert.Equal("grades.not_your_class", await enter.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, book.StatusCode);
        Assert.Equal("grades.not_homeroom", await publish.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, principalBook.StatusCode);
    }

    [Fact]
    public async Task Published_grades_are_locked_until_the_homeroom_teacher_unpublishes_them()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        var termId = await ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        var submission = await ScoredAssignmentAsync(scenario, 0, 75);

        var published = await PublishAsync(teacher, scenario, termId);
        var again = await PublishAsync(teacher, scenario, termId);
        var enter = await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Quiz, 80);
        var rescore = await teacher.PutJsonAsync($"/api/v1/assignments/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, 99, null));
        var book = await (await teacher.GetAsync(BookUrl(scenario, termId))).ReadAsync<GradeBookDto>();

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal("grades.already_published", await again.ProblemCodeAsync());
        Assert.Equal((HttpStatusCode.Conflict, "grades.published"), (enter.StatusCode, await enter.ProblemCodeAsync()));
        Assert.Equal((HttpStatusCode.Conflict, "grades.published"), (rescore.StatusCode, await rescore.ProblemCodeAsync()));
        Assert.True(book.IsPublished);

        var unpublished = await teacher.DeleteAsync($"{Url}/publications?classroomId={scenario.Classroom.Id}&termId={termId}");
        var afterwards = await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Quiz, 80);
        var notPublished = await teacher.DeleteAsync($"{Url}/publications?classroomId={scenario.Classroom.Id}&termId={termId}");

        Assert.Equal(HttpStatusCode.NoContent, unpublished.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
        Assert.Equal("grades.not_published", await notPublished.ProblemCodeAsync());
    }

    [Fact]
    public async Task The_teacher_of_the_subject_sets_its_weights_and_they_change_the_final_grade()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday, subjectName: "Arts and Culture");
        var stranger = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: DayOfWeek.Sunday, subjectName: "Civics");
        var termId = await ActiveTermIdAsync(fixture);
        using var teacher = scenario.TeacherClient();
        using var other = stranger.TeacherClient();
        using var principal = fixture.AsPrincipal();
        var weightsUrl = $"/api/v1/grade-weights/{scenario.Subject.Id}";
        await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Quiz, 100);
        await EnterAsync(teacher, scenario, termId, 0, GradeComponent.Final, 50);

        var before = await (await principal.GetAsync(weightsUrl)).ReadAsync<GradeWeightsDto>();
        var notTheirs = await other.PutJsonAsync(weightsUrl, Weights(25, 25, 25, 25));
        var notTheirsRead = await other.GetAsync(weightsUrl);
        var invalid = await teacher.PutJsonAsync(weightsUrl, Weights(25, 25, 25, 20));
        var set = await teacher.PutJsonAsync(weightsUrl, Weights(10, 50, 10, 30));
        var after = await set.ReadAsync<GradeWeightsDto>();
        var row = Assert.Single((await (await teacher.GetAsync(BookUrl(scenario, termId))).ReadAsync<GradeBookDto>()).Rows);

        Assert.True(before.IsDefault);
        Assert.Equal(30m, before.Weights.Single(w => w.Component == GradeComponent.Final).WeightPercent);
        Assert.Equal("grades.not_your_subject", await notTheirs.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, notTheirsRead.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.False(after.IsDefault);
        Assert.Equal(50m, after.Weights.Single(w => w.Component == GradeComponent.Quiz).WeightPercent);
        // (100*50 + 50*30) / 80 = 81.25
        Assert.Equal(81.25m, row.FinalGrade);

        await teacher.PutJsonAsync(weightsUrl, Weights(30, 20, 20, 30)); // the subject is shared by other tests
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Admin")]
    [InlineData("Principal")]
    public async Task Only_teachers_set_weights(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.PutJsonAsync($"/api/v1/grade-weights/{Guid.NewGuid()}", Weights(30, 20, 20, 30));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static SetGradeWeightsCommand Weights(decimal assignment, decimal quiz, decimal midterm, decimal final) =>
        new(Guid.Empty, [new(GradeComponent.Assignment, assignment), new(GradeComponent.Quiz, quiz), new(GradeComponent.Midterm, midterm), new(GradeComponent.Final, final)]);
}
