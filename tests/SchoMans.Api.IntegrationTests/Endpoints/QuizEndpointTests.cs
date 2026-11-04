using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.QuestionBank;
using SchoMans.Application.Features.Quizzes;
using SchoMans.Domain.Assignments;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Grading;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// AI-06, second half (online quizzes): a teacher builds a quiz from the question bank, each student takes it once before the due date,
/// multiple choice is scored at once, short answers get points from the teacher, and the score enters the Quiz grade. A student sees no
/// result, no key and no explanation before the due date.
/// </summary>
[Collection(ApiCollection.Name)]
public class QuizEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/quizzes";

    private static DateTimeOffset InThreeDays => DateTimeOffset.UtcNow.AddDays(3);

    private static QuestionInput Choice(string body, int key) => new(QuestionType.MultipleChoice, QuestionDifficulty.Easy, body, ["a", "b", "c"], key.ToString(), $"Because of {body}");

    private static QuestionInput Short(string body) => new(QuestionType.ShortAnswer, QuestionDifficulty.Easy, body, [], "model answer", "Explained.");

    private sealed record Quiz(ClassroomScenario Scenario, List<QuestionDto> Bank, AssignmentDto Assignment)
    {
        public Guid Id => Assignment.Id;

        public QuestionDto Mc1 => Bank[0]; // right option: 1

        public QuestionDto Mc2 => Bank[1]; // right option: 0

        public QuestionDto Sa => Bank[2];
    }

    private async Task<List<QuestionDto>> BankAsync(HttpClient teacher, Guid subjectId, params QuestionInput[] questions)
    {
        var response = await teacher.PostJsonAsync("/api/v1/question-bank", new SaveQuestionsCommand(subjectId, [.. questions]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<List<QuestionDto>>();
    }

    /// <summary>A quiz of two multiple choice questions (1 point each) and a short answer (2 points): 4 points.</summary>
    private async Task<Quiz> MixedQuizAsync(int students = 2, DateTimeOffset? due = null)
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, students);
        using var teacher = scenario.TeacherClient();
        var bank = await BankAsync(teacher, scenario.Subject.Id, Choice("First?", 1), Choice("Second?", 0), Short("Explain."));
        var assignment = await CreateAsync(teacher, scenario, [new(bank[0].Id, 1), new(bank[1].Id, 1), new(bank[2].Id, 2)], due);
        return new Quiz(scenario, bank, assignment);
    }

    /// <summary>A quiz of two multiple choice questions only (1 point each): scored at once.</summary>
    private async Task<Quiz> ChoiceQuizAsync(int students = 2, DateTimeOffset? due = null)
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, students);
        using var teacher = scenario.TeacherClient();
        var bank = await BankAsync(teacher, scenario.Subject.Id, Choice("First?", 1), Choice("Second?", 0));
        var assignment = await CreateAsync(teacher, scenario, [new(bank[0].Id, 1), new(bank[1].Id, 1)], due);
        return new Quiz(scenario, bank, assignment);
    }

    private static async Task<AssignmentDto> CreateAsync(HttpClient teacher, ClassroomScenario scenario, List<QuizQuestionInput> questions, DateTimeOffset? due = null)
    {
        var response = await teacher.PostJsonAsync(Url, new CreateQuizCommand("Cells quiz", "Chapter 3", due ?? InThreeDays, scenario.Subject.Id, [scenario.Classroom.Id], questions));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<AssignmentDto>();
    }

    private static Task<HttpResponseMessage> TakeAsync(HttpClient student, Guid quizId, params (Guid Question, string? Answer)[] answers) =>
        student.PostJsonAsync($"{Url}/{quizId}/submit", new SubmitQuizCommand(quizId, [.. answers.Select(a => new QuizAnswerInput(a.Question, a.Answer))]));

    private static async Task<QuizDto> ReadQuizAsync(HttpClient client, Guid quizId, Guid? studentId = null)
    {
        var response = await client.GetAsync($"{Url}/{quizId}" + (studentId is null ? string.Empty : $"?studentId={studentId}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<QuizDto>();
    }

    private static Task<HttpResponseMessage> GradeAsync(HttpClient teacher, Guid quizId, Guid studentId, params (Guid Question, decimal Points)[] points) =>
        teacher.PostJsonAsync($"{Url}/{quizId}/grade", new GradeQuizCommand(quizId, studentId, [.. points.Select(p => new ShortPointsInput(p.Question, p.Points))]));

    /// <summary>A second host on the same database whose clock is after the due date.</summary>
    private ApiFactory After(out TestClock clock)
    {
        clock = new TestClock(DateTimeOffset.UtcNow.AddDays(10));
        return fixture.CreateFactory(clock);
    }

    private static HttpClient StudentOn(ApiFactory factory, ClassroomScenario scenario, int index) =>
        factory.AsUser(scenario.Students[index].Email, scenario.Students[index].FullName, $"sub-{scenario.Students[index].Email}", "Student");

    private static HttpClient TeacherOn(ApiFactory factory, ClassroomScenario scenario) =>
        factory.AsUser(scenario.Teacher.Email, scenario.Teacher.FullName, $"sub-{scenario.Teacher.Email}", "Teacher");

    private async Task<List<Grade>> GradesOfAsync(Guid submissionId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Grades.AsNoTracking().Where(g => g.SubmissionId == submissionId).ToListAsync();
    }

    // ---- building a quiz ----

    [Fact]
    public async Task A_teacher_builds_a_quiz_from_the_bank_and_it_is_an_assignment_of_type_quiz_for_the_classroom()
    {
        var quiz = await MixedQuizAsync();

        Assert.Equal(AssignmentType.Quiz, quiz.Assignment.Type);
        Assert.Equal(["Cells quiz"], [quiz.Assignment.Title]);
        using var student = quiz.Scenario.StudentClient(0);
        var seen = await (await student.GetAsync($"/api/v1/assignments/{quiz.Id}")).ReadAsync<AssignmentDto>();
        Assert.Equal(AssignmentType.Quiz, seen.Type);
        var list = await (await student.GetAsync("/api/v1/assignments/mine?pageSize=100")).ReadAsync<PagedResult<StudentAssignmentDto>>();
        Assert.Contains(list.Items, a => a.Id == quiz.Id);
    }

    [Fact]
    public async Task The_students_of_the_classroom_are_told_about_the_new_quiz()
    {
        var quiz = await ChoiceQuizAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == quiz.Scenario.Students[0].Email).Select(u => u.Id).SingleAsync();

        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == userId && n.LinkUrl.Contains(quiz.Id.ToString())));
    }

    [Fact]
    public async Task The_questions_keep_the_order_and_the_points_the_teacher_chose()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var bank = await BankAsync(teacher, scenario.Subject.Id, Choice("One?", 1), Choice("Two?", 1), Choice("Three?", 1));
        var assignment = await CreateAsync(teacher, scenario, [new(bank[2].Id, 5), new(bank[0].Id, 1), new(bank[1].Id, 3)]);

        var asked = await ReadQuizAsync(teacher, assignment.Id);

        Assert.Equal(["Three?", "One?", "Two?"], asked.Questions.Select(q => q.Body));
        Assert.Equal([1, 2, 3], asked.Questions.Select(q => q.Order));
        Assert.Equal([5, 1, 3], asked.Questions.Select(q => q.Points));
        Assert.Equal(9, asked.TotalPoints);
    }

    [Fact]
    public async Task An_invalid_quiz_is_a_validation_error()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var bank = await BankAsync(teacher, scenario.Subject.Id, Choice("One?", 1));
        var one = new QuizQuestionInput(bank[0].Id);

        CreateQuizCommand Make(List<QuizQuestionInput>? questions = null, string title = "Quiz", List<Guid>? classrooms = null, DateTimeOffset? due = null) =>
            new(title, null, due ?? InThreeDays, scenario.Subject.Id, classrooms ?? [scenario.Classroom.Id], questions ?? [one]);

        CreateQuizCommand[] cases =
        [
            Make(questions: []),
            Make(questions: [one, one]),
            Make(questions: [new(bank[0].Id, 0)]),
            Make(questions: [new(bank[0].Id, 101)]),
            Make(title: " "),
            Make(classrooms: []),
            Make(due: DateTimeOffset.UtcNow.AddHours(-1)),
        ];

        foreach (var command in cases)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostJsonAsync(Url, command)).StatusCode);
        }
    }

    [Fact]
    public async Task A_question_from_another_subject_or_that_does_not_exist_is_refused()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 1, subjectName: "English");
        using var teacher = scenario.TeacherClient();
        using var otherTeacher = other.TeacherClient();
        var foreign = await BankAsync(otherTeacher, other.Subject.Id, Choice("English?", 1));

        var wrongSubject = await teacher.PostJsonAsync(Url, new CreateQuizCommand("Quiz", null, InThreeDays, scenario.Subject.Id, [scenario.Classroom.Id], [new(foreign[0].Id)]));
        var missing = await teacher.PostJsonAsync(Url, new CreateQuizCommand("Quiz", null, InThreeDays, scenario.Subject.Id, [scenario.Classroom.Id], [new(Guid.NewGuid())]));

        Assert.Equal(HttpStatusCode.BadRequest, wrongSubject.StatusCode);
        Assert.Equal("quizzes.question_other_subject", await wrongSubject.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("quizzes.question_not_found", await missing.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_teacher_can_only_give_a_quiz_to_classrooms_they_teach_the_subject_in()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var bank = await BankAsync(teacher, scenario.Subject.Id, Choice("One?", 1));

        var response = await teacher.PostJsonAsync(Url, new CreateQuizCommand("Quiz", null, InThreeDays, scenario.Subject.Id, [other.Classroom.Id], [new(bank[0].Id)]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("assignments.not_your_class", await response.ProblemCodeAsync());
    }

    // ---- taking it ----

    [Fact]
    public async Task A_student_sees_the_questions_without_the_key_and_the_explanations_while_the_quiz_is_open()
    {
        var quiz = await MixedQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);

        var asked = await ReadQuizAsync(student, quiz.Id);

        Assert.True(asked.IsOpen);
        Assert.False(asked.ResultsVisible);
        Assert.Equal(3, asked.Questions.Count);
        Assert.All(asked.Questions, q =>
        {
            Assert.Null(q.AnswerKey);
            Assert.Null(q.Explanation);
            Assert.Null(q.MyAnswer);
            Assert.Null(q.PointsAwarded);
        });
        Assert.Equal(["a", "b", "c"], asked.Questions[0].Options);
        Assert.Null(asked.Attempt);
    }

    [Fact]
    public async Task Multiple_choice_is_scored_at_once_and_becomes_a_quiz_grade_but_the_student_sees_nothing_before_the_due_date()
    {
        var quiz = await ChoiceQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);

        var response = await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"), (quiz.Mc2.Id, "2")); // one right, one wrong

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var taken = await response.ReadAsync<QuizDto>();
        Assert.Equal(QuizAttemptStatus.Submitted, taken.Attempt!.Status);
        Assert.Null(taken.Attempt.Score);
        Assert.All(taken.Questions, q => Assert.Null(q.AnswerKey));
        Assert.Equal(["1", "2"], taken.Questions.Select(q => q.MyAnswer));

        var grade = Assert.Single(await GradesOfAsync(taken.Attempt.SubmissionId));
        Assert.Equal((GradeComponent.Quiz, 50m), (grade.Component, grade.Score));

        // The generic endpoints keep the result back too.
        var work = await (await student.GetAsync($"/api/v1/assignments/{quiz.Id}")).ReadAsync<AssignmentDto>();
        Assert.Null(work.MySubmission!.Score);
        Assert.Null(work.MySubmission.GradedAt);
        var list = await (await student.GetAsync("/api/v1/assignments/mine?pageSize=100")).ReadAsync<PagedResult<StudentAssignmentDto>>();
        Assert.Null(list.Items.Single(a => a.Id == quiz.Id).Score);
        var submission = await (await student.GetAsync($"/api/v1/assignments/submissions/{taken.Attempt.SubmissionId}")).ReadAsync<SubmissionDto>();
        Assert.Null(submission.Score);
    }

    [Fact]
    public async Task After_the_due_date_the_student_sees_the_score_the_key_the_explanations_and_the_points()
    {
        var quiz = await ChoiceQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);
        await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"), (quiz.Mc2.Id, "2"));

        await using var later = After(out _);
        using var studentLater = StudentOn(later, quiz.Scenario, 0);
        var result = await ReadQuizAsync(studentLater, quiz.Id);

        Assert.True(result.ResultsVisible);
        Assert.False(result.IsOpen);
        Assert.Equal((QuizAttemptStatus.Scored, 50m, 0), (result.Attempt!.Status, result.Attempt.Score, result.Attempt.ShortAnswersToGrade));
        Assert.Equal(["1", "0"], result.Questions.Select(q => q.AnswerKey));
        Assert.Equal(["Because of First?", "Because of Second?"], result.Questions.Select(q => q.Explanation));
        Assert.Equal([1m, 0m], result.Questions.Select(q => q.PointsAwarded));
        var list = await (await studentLater.GetAsync("/api/v1/assignments/mine?pageSize=100")).ReadAsync<PagedResult<StudentAssignmentDto>>();
        Assert.Equal(50m, list.Items.Single(a => a.Id == quiz.Id).Score);
    }

    [Fact]
    public async Task A_quiz_is_taken_once()
    {
        var quiz = await ChoiceQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);
        (await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"))).EnsureSuccessStatusCode();

        var again = await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "0"));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("quizzes.already_submitted", await again.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_quiz_cannot_be_taken_after_the_due_date()
    {
        var quiz = await ChoiceQuizAsync();
        await using var later = After(out _);
        using var student = StudentOn(later, quiz.Scenario, 0);

        var response = await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("quizzes.closed", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Questions_left_empty_count_as_wrong()
    {
        var quiz = await ChoiceQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);

        var taken = await (await TakeAsync(student, quiz.Id)).ReadAsync<QuizDto>();

        var grade = Assert.Single(await GradesOfAsync(taken.Attempt!.SubmissionId));
        Assert.Equal(0m, grade.Score);
    }

    [Fact]
    public async Task Invalid_answers_are_refused_and_nothing_is_stored()
    {
        var quiz = await ChoiceQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);

        (Guid, string?)[][] cases =
        [
            [(Guid.NewGuid(), "1")], // not a question of this quiz
            [(quiz.Mc1.Id, "1"), (quiz.Mc1.Id, "0")], // the same question twice
            [(quiz.Mc1.Id, "3")], // past the options
            [(quiz.Mc1.Id, "-1")],
            [(quiz.Mc1.Id, "b")],
        ];

        foreach (var answers in cases)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await TakeAsync(student, quiz.Id, answers)).StatusCode);
        }

        Assert.Null((await ReadQuizAsync(student, quiz.Id)).Attempt);
    }

    [Fact]
    public async Task A_quiz_is_taken_by_students_of_its_classroom_only()
    {
        var quiz = await ChoiceQuizAsync();
        var outsider = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var student = outsider.StudentClient(0);

        Assert.Equal(HttpStatusCode.Forbidden, (await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"{Url}/{quiz.Id}")).StatusCode);
    }

    // ---- short answers ----

    [Fact]
    public async Task A_quiz_with_a_short_answer_waits_for_the_teacher_and_makes_no_grade_yet()
    {
        var quiz = await MixedQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);
        using var teacher = quiz.Scenario.TeacherClient();
        var taken = await (await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"), (quiz.Mc2.Id, "0"), (quiz.Sa.Id, "my answer"))).ReadAsync<QuizDto>();

        var asTeacher = await ReadQuizAsync(teacher, quiz.Id, quiz.Scenario.Students[0].Id);

        Assert.Empty(await GradesOfAsync(taken.Attempt!.SubmissionId));
        Assert.Equal((QuizAttemptStatus.WaitingForTeacher, 1, null), (asTeacher.Attempt!.Status, asTeacher.Attempt.ShortAnswersToGrade, asTeacher.Attempt.Score));
        Assert.Equal(["1", "0", "model answer"], asTeacher.Questions.Select(q => q.AnswerKey));
        Assert.Equal("my answer", asTeacher.Questions[2].MyAnswer);
        Assert.Equal([1m, 1m, null], asTeacher.Questions.Select(q => q.PointsAwarded));
    }

    [Fact]
    public async Task The_teacher_gives_points_to_the_short_answer_and_the_quiz_is_scored_graded_and_the_student_told()
    {
        var quiz = await MixedQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);
        using var teacher = quiz.Scenario.TeacherClient();
        var taken = await (await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"), (quiz.Mc2.Id, "2"), (quiz.Sa.Id, "my answer"))).ReadAsync<QuizDto>();

        var graded = await GradeAsync(teacher, quiz.Id, quiz.Scenario.Students[0].Id, (quiz.Sa.Id, 1.5m));

        Assert.Equal(HttpStatusCode.OK, graded.StatusCode);
        var result = await graded.ReadAsync<QuizDto>();
        Assert.Equal((QuizAttemptStatus.Scored, 62.5m, 0), (result.Attempt!.Status, result.Attempt.Score, result.Attempt.ShortAnswersToGrade)); // (1 + 0 + 1.5) of 4
        var grade = Assert.Single(await GradesOfAsync(taken.Attempt!.SubmissionId));
        Assert.Equal((GradeComponent.Quiz, 62.5m), (grade.Component, grade.Score));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == quiz.Scenario.Students[0].Email).Select(u => u.Id).SingleAsync();
        var told = await db.Notifications.Where(n => n.UserId == userId && n.Type == NotificationType.AssignmentGraded).ToListAsync();
        var note = Assert.Single(told);
        Assert.DoesNotContain("62", note.Body, StringComparison.Ordinal); // the score is not told before the due date
    }

    [Fact]
    public async Task Points_may_be_corrected_and_the_grade_follows()
    {
        var quiz = await MixedQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);
        using var teacher = quiz.Scenario.TeacherClient();
        var taken = await (await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"), (quiz.Mc2.Id, "0"), (quiz.Sa.Id, "answer"))).ReadAsync<QuizDto>();
        (await GradeAsync(teacher, quiz.Id, quiz.Scenario.Students[0].Id, (quiz.Sa.Id, 0m))).EnsureSuccessStatusCode();

        var corrected = await GradeAsync(teacher, quiz.Id, quiz.Scenario.Students[0].Id, (quiz.Sa.Id, 2m));

        Assert.Equal(100m, (await corrected.ReadAsync<QuizDto>()).Attempt!.Score);
        Assert.Equal(100m, Assert.Single(await GradesOfAsync(taken.Attempt!.SubmissionId)).Score);
    }

    [Fact]
    public async Task Invalid_points_are_refused()
    {
        var quiz = await MixedQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);
        using var teacher = quiz.Scenario.TeacherClient();
        await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"), (quiz.Sa.Id, "answer"));
        var studentId = quiz.Scenario.Students[0].Id;

        var tooMany = await GradeAsync(teacher, quiz.Id, studentId, (quiz.Sa.Id, 2.5m));
        var negative = await GradeAsync(teacher, quiz.Id, studentId, (quiz.Sa.Id, -1m));
        var choice = await GradeAsync(teacher, quiz.Id, studentId, (quiz.Mc1.Id, 1m));
        var unknown = await GradeAsync(teacher, quiz.Id, studentId, (Guid.NewGuid(), 1m));
        var none = await GradeAsync(teacher, quiz.Id, studentId);

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal("quizzes.points_out_of_range", await tooMany.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal("quizzes.not_a_short_answer", await choice.ProblemCodeAsync());
        Assert.Equal("quizzes.not_a_short_answer", await unknown.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
    }

    [Fact]
    public async Task A_student_who_has_not_taken_the_quiz_has_nothing_to_score()
    {
        var quiz = await MixedQuizAsync();
        using var teacher = quiz.Scenario.TeacherClient();

        var response = await GradeAsync(teacher, quiz.Id, quiz.Scenario.Students[1].Id, (quiz.Sa.Id, 1m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("quizzes.no_attempt", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Only_the_teacher_of_the_quiz_scores_it_and_only_students_take_it()
    {
        var quiz = await MixedQuizAsync();
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var student = quiz.Scenario.StudentClient(0);
        using var otherTeacher = other.TeacherClient();
        using var parent = fixture.AsParent();
        using var teacher = quiz.Scenario.TeacherClient();
        await TakeAsync(student, quiz.Id, (quiz.Sa.Id, "answer"));
        var studentId = quiz.Scenario.Students[0].Id;

        Assert.Equal(HttpStatusCode.Forbidden, (await GradeAsync(otherTeacher, quiz.Id, studentId, (quiz.Sa.Id, 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GradeAsync(student, quiz.Id, studentId, (quiz.Sa.Id, 2m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GradeAsync(parent, quiz.Id, studentId, (quiz.Sa.Id, 2m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TakeAsync(teacher, quiz.Id, (quiz.Sa.Id, "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherTeacher.GetAsync($"{Url}/{quiz.Id}")).StatusCode);
    }

    // ---- who sees what ----

    [Fact]
    public async Task A_student_never_sees_the_attempt_of_a_classmate()
    {
        var quiz = await ChoiceQuizAsync();
        using var first = quiz.Scenario.StudentClient(0);
        using var second = quiz.Scenario.StudentClient(1);
        await TakeAsync(first, quiz.Id, (quiz.Mc1.Id, "1"));

        var asked = await ReadQuizAsync(second, quiz.Id, quiz.Scenario.Students[0].Id); // names the classmate

        Assert.Null(asked.Attempt);
    }

    [Fact]
    public async Task The_principal_reads_a_quiz_with_the_key_but_cannot_score_it()
    {
        var quiz = await MixedQuizAsync();
        using var principal = fixture.AsPrincipal();

        var asked = await ReadQuizAsync(principal, quiz.Id);

        Assert.False(asked.CanEdit);
        Assert.Equal("1", asked.Questions[0].AnswerKey);
        Assert.Equal(HttpStatusCode.Forbidden, (await GradeAsync(principal, quiz.Id, quiz.Scenario.Students[0].Id, (quiz.Sa.Id, 1m))).StatusCode);
    }

    [Fact]
    public async Task A_quiz_does_not_exist_for_an_ordinary_assignment_and_ordinary_endpoints_refuse_a_quiz()
    {
        var quiz = await MixedQuizAsync();
        using var student = quiz.Scenario.StudentClient(0);
        using var teacher = quiz.Scenario.TeacherClient();
        var essay = await (await teacher.PostJsonAsync("/api/v1/assignments", new SchoMans.Application.Features.Assignments.CreateAssignment.CreateAssignmentCommand(
            "Essay", "Write.", InThreeDays, quiz.Scenario.Subject.Id, [quiz.Scenario.Classroom.Id], null))).ReadAsync<AssignmentDto>();
        await TakeAsync(student, quiz.Id, (quiz.Sa.Id, "answer"));
        var submissionId = (await ReadQuizAsync(teacher, quiz.Id, quiz.Scenario.Students[0].Id)).Attempt!.SubmissionId;

        var notAQuiz = await teacher.GetAsync($"{Url}/{essay.Id}");
        var handIn = await student.PutJsonAsync($"/api/v1/assignments/{quiz.Id}/submission", new SchoMans.Application.Features.Assignments.SubmitAssignment.SubmitAssignmentCommand(quiz.Id, null, null, "text"));
        var score = await teacher.PutJsonAsync($"/api/v1/assignments/submissions/{submissionId}/score", new SchoMans.Application.Features.Assignments.GradeSubmission.GradeSubmissionCommand(submissionId, 80, null));

        Assert.Equal(HttpStatusCode.NotFound, notAQuiz.StatusCode);
        Assert.Equal("quizzes.use_quiz", await handIn.ProblemCodeAsync());
        Assert.Equal("quizzes.scored_by_questions", await score.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_question_in_a_quiz_can_no_longer_be_changed_or_deleted()
    {
        var quiz = await ChoiceQuizAsync();
        using var teacher = quiz.Scenario.TeacherClient();

        var change = await teacher.PutJsonAsync($"/api/v1/question-bank/{quiz.Mc1.Id}", new UpdateQuestionCommand(quiz.Mc1.Id, Choice("Changed?", 0)));
        var delete = await teacher.DeleteAsync($"/api/v1/question-bank/{quiz.Mc1.Id}");

        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
        Assert.Equal("questions.in_use", await change.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Several_quizzes_each_make_their_own_quiz_grade_for_the_subject()
    {
        var first = await ChoiceQuizAsync(1);
        using var student = first.Scenario.StudentClient(0);
        using var teacher = first.Scenario.TeacherClient();
        var bank = await BankAsync(teacher, first.Scenario.Subject.Id, Choice("Third?", 2));
        var second = await CreateAsync(teacher, first.Scenario, [new(bank[0].Id)]);

        var one = await (await TakeAsync(student, first.Id, (first.Mc1.Id, "1"), (first.Mc2.Id, "0"))).ReadAsync<QuizDto>();
        var two = await (await TakeAsync(student, second.Id, (bank[0].Id, "0"))).ReadAsync<QuizDto>();

        Assert.Equal(100m, Assert.Single(await GradesOfAsync(one.Attempt!.SubmissionId)).Score);
        Assert.Equal(0m, Assert.Single(await GradesOfAsync(two.Attempt!.SubmissionId)).Score);
        // The grade book averages the scores of a component, so the Quiz component of this student is 50.
        Assert.Equal(50m, GradeCalculator.FinalGrade(
            new Dictionary<GradeComponent, decimal> { [GradeComponent.Quiz] = 100m },
            [(GradeComponent.Quiz, 100m), (GradeComponent.Quiz, 0m)]));
    }

    [Fact]
    public async Task Parents_read_the_quiz_of_their_child_without_the_result_before_the_due_date()
    {
        var quiz = await ChoiceQuizAsync();
        // A parent of its own: the demo parent has exactly two children, which other tests count on.
        var key = Guid.NewGuid().ToString("N");
        var email = $"parent-{key}@test.example";
        using var parent = fixture.AsUser(email, "Pia Parent", $"sub-{key}", "Parent");
        (await parent.GetAsync("/api/v1/announcements/mine")).EnsureSuccessStatusCode(); // the first request creates the user record
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var parentId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            db.StudentParents.Add(SchoMans.Domain.Students.StudentParent.Create(parentId, quiz.Scenario.Students[0].Id));
            await db.SaveChangesAsync();
        }

        using var student = quiz.Scenario.StudentClient(0);
        await TakeAsync(student, quiz.Id, (quiz.Mc1.Id, "1"), (quiz.Mc2.Id, "0"));

        var seen = await ReadQuizAsync(parent, quiz.Id, quiz.Scenario.Students[0].Id);

        Assert.Equal(QuizAttemptStatus.Submitted, seen.Attempt!.Status);
        Assert.Null(seen.Attempt.Score);
        Assert.All(seen.Questions, q => Assert.Null(q.AnswerKey));

        await using var later = After(out _);
        using var parentLater = later.AsUser(email, "Pia Parent", $"sub-{key}", "Parent");
        var result = await ReadQuizAsync(parentLater, quiz.Id, quiz.Scenario.Students[0].Id);
        Assert.Equal(100m, result.Attempt!.Score);
    }

    [Fact]
    public async Task Guests_are_turned_away()
    {
        var quiz = await ChoiceQuizAsync(1);
        using var guest = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"{Url}/{quiz.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TakeAsync(guest, quiz.Id)).StatusCode);
    }
}
