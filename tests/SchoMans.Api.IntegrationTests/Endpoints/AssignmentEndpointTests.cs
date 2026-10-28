using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.Assignments.CreateAssignment;
using SchoMans.Application.Features.Assignments.CreateAttachmentUploadUrl;
using SchoMans.Application.Features.Assignments.CreateSubmissionUploadUrl;
using SchoMans.Application.Features.Assignments.GradeSubmission;
using SchoMans.Application.Features.Assignments.SendDueReminders;
using SchoMans.Application.Features.Assignments.SubmitAssignment;
using SchoMans.Application.Features.Assignments.UpdateAssignment;
using SchoMans.Application.Features.Files.CreateUploadUrl;
using SchoMans.Application.Features.Notifications;
using SchoMans.Application.Features.Students.ListMyChildren;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>"Assignment flows pass": a teacher gives work with attachments, students hand it in, the teacher scores it.</summary>
[Collection(ApiCollection.Name)]
public class AssignmentEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/assignments";

    private static DateTimeOffset InTwoDays => DateTimeOffset.UtcNow.AddDays(2);

    private async Task<(ClassroomScenario Scenario, AssignmentDto Assignment)> GiveAsync(int students = 2, DateTimeOffset? due = null, IReadOnlyList<NewAttachment>? attachments = null)
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, students, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        var response = await teacher.PostJsonAsync(Url, new CreateAssignmentCommand(
            "Fractions", "Do page 12", due ?? InTwoDays, scenario.Subject.Id, [scenario.Classroom.Id], attachments));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (scenario, await response.ReadAsync<AssignmentDto>());
    }

    private async Task<string> UploadAttachmentAsync(HttpClient teacher, string contentType = "application/pdf", long size = 2048)
    {
        var upload = await (await teacher.PostJsonAsync($"{Url}/attachments/upload-url", new CreateAttachmentUploadUrlCommand(contentType, size))).ReadAsync<UploadUrlDto>();
        fixture.Storage.Put(FileBucket.Private, upload.Key, size, contentType);
        return upload.Key;
    }

    private async Task<SubmissionDto> SubmitTextAsync(HttpClient student, Guid assignmentId, string text = "My answer")
    {
        var response = await student.PutJsonAsync($"{Url}/{assignmentId}/submission", new SubmitAssignmentCommand(assignmentId, null, null, text));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<SubmissionDto>();
    }

    [Fact]
    public async Task A_teacher_may_give_work_for_the_classrooms_and_subjects_of_their_timetable()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();

        var options = await (await teacher.GetAsync($"{Url}/teaching-options")).ReadAsync<List<TeachingOptionDto>>();
        var classB = await TestData.ClassroomAsync(fixture.AsAdmin(), "Class B");
        var notTaught = await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("X", null, InTwoDays, scenario.Subject.Id, [classB.Id], null));

        var option = Assert.Single(options);
        Assert.Equal(scenario.Classroom.Id, option.ClassroomId);
        Assert.Equal(scenario.Subject.Name, option.SubjectName);
        Assert.Equal(HttpStatusCode.Forbidden, notTaught.StatusCode);
        Assert.Equal("assignments.not_your_class", await notTaught.ProblemCodeAsync());
    }

    [Fact]
    public async Task Giving_an_assignment_saves_its_attachments_and_notifies_the_students()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 2, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        var key = await UploadAttachmentAsync(teacher);

        var response = await teacher.PostJsonAsync(Url, new CreateAssignmentCommand(
            "Fractions", "Do page 12", InTwoDays, scenario.Subject.Id, [scenario.Classroom.Id], [new NewAttachment(key, "page-12.pdf")]));
        var assignment = await response.ReadAsync<AssignmentDto>();
        using var student = scenario.StudentClient(0);
        var notifications = await (await student.GetAsync("/api/v1/notifications?pageSize=10")).ReadAsync<PagedResult<NotificationDto>>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("page-12.pdf", Assert.Single(assignment.Attachments).FileName);
        Assert.True(assignment.CanEdit);
        Assert.Equal("Term 1", assignment.TermName);
        var notice = Assert.Single(notifications.Items, n => n.LinkUrl == $"/assignments/{assignment.Id}");
        Assert.Equal("New assignment: Fractions", notice.Title);
        Assert.StartsWith($"{scenario.Subject.Name}, due ", notice.Body);
    }

    [Fact]
    public async Task An_attachment_must_be_the_teacher_s_own_upload_and_really_uploaded()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        var notUploaded = (await (await teacher.PostJsonAsync($"{Url}/attachments/upload-url", new CreateAttachmentUploadUrlCommand("application/pdf", 10))).ReadAsync<UploadUrlDto>()).Key;

        var foreign = await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("X", null, InTwoDays, scenario.Subject.Id, [scenario.Classroom.Id], [new NewAttachment("assignments/uploads/someone-else/x.pdf", "x.pdf")]));
        var missing = await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("X", null, InTwoDays, scenario.Subject.Id, [scenario.Classroom.Id], [new NewAttachment(notUploaded, "x.pdf")]));
        var badType = await teacher.PostJsonAsync($"{Url}/attachments/upload-url", new CreateAttachmentUploadUrlCommand("application/x-msdownload", 10));

        Assert.Equal("files.invalid_key", await foreign.ProblemCodeAsync());
        Assert.Equal("files.not_uploaded", await missing.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
    }

    [Fact]
    public async Task An_assignment_due_in_the_past_or_without_classrooms_is_a_validation_error()
    {
        using var teacher = fixture.AsTeacher();

        var past = await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("X", null, DateTimeOffset.UtcNow.AddMinutes(-1), Guid.NewGuid(), [Guid.NewGuid()], null));
        var noClass = await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("X", null, InTwoDays, Guid.NewGuid(), [], null));

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noClass.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("Principal")]
    public async Task Only_teachers_give_assignments(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.PostJsonAsync(Url, new CreateAssignmentCommand("X", null, InTwoDays, Guid.NewGuid(), [Guid.NewGuid()], null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Students_see_their_assignments_nearest_due_first_with_their_status()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        foreach (var (title, days) in new[] { ("Later", 5), ("Sooner", 1), ("Middle", 3) })
        {
            (await teacher.PostJsonAsync(Url, new CreateAssignmentCommand(title, null, DateTimeOffset.UtcNow.AddDays(days), scenario.Subject.Id, [scenario.Classroom.Id], null))).EnsureSuccessStatusCode();
        }

        using var student = scenario.StudentClient(0);
        var before = await (await student.GetAsync($"{Url}/mine")).ReadAsync<PagedResult<StudentAssignmentDto>>();
        await SubmitTextAsync(student, before.Items[0].Id);
        var after = await (await student.GetAsync($"{Url}/mine")).ReadAsync<PagedResult<StudentAssignmentDto>>();

        Assert.Equal(["Sooner", "Middle", "Later"], before.Items.Select(a => a.Title));
        Assert.All(before.Items, a => Assert.Equal(StudentAssignmentStatus.NotSubmitted, a.Status));
        Assert.Equal(StudentAssignmentStatus.Submitted, after.Items[0].Status);
        Assert.NotNull(after.Items[0].SubmittedAt);
    }

    [Fact]
    public async Task A_student_hands_in_a_file_and_can_replace_it_until_it_is_scored()
    {
        var (scenario, assignment) = await GiveAsync();
        using var student = scenario.StudentClient(0);
        using var teacher = scenario.TeacherClient();

        var upload = await (await student.PostJsonAsync($"{Url}/{assignment.Id}/submission/upload-url", new CreateSubmissionUploadUrlCommand(assignment.Id, "application/pdf", 5000))).ReadAsync<UploadUrlDto>();
        fixture.Storage.Put(FileBucket.Private, upload.Key, 5000, "application/pdf");
        var first = await (await student.PutJsonAsync($"{Url}/{assignment.Id}/submission", new SubmitAssignmentCommand(assignment.Id, upload.Key, "answer.pdf", null))).ReadAsync<SubmissionDto>();
        var replaced = await SubmitTextAsync(student, assignment.Id, "Typed instead");

        Assert.True(first.HasFile);
        Assert.False(first.IsLate);
        Assert.False(replaced.HasFile);
        Assert.Equal("Typed instead", replaced.TextBody);
        Assert.Equal(first.Id, replaced.Id);
        Assert.False(fixture.Storage.Contains(FileBucket.Private, upload.Key)); // the replaced file is gone

        var graded = await (await teacher.PutJsonAsync($"{Url}/submissions/{replaced.Id}/score", new GradeSubmissionCommand(replaced.Id, 88.5m, "Well explained"))).ReadAsync<SubmissionDto>();
        var afterScore = await student.PutJsonAsync($"{Url}/{assignment.Id}/submission", new SubmitAssignmentCommand(assignment.Id, null, null, "Too late to change"));
        var detail = await (await student.GetAsync($"{Url}/{assignment.Id}")).ReadAsync<AssignmentDto>();

        Assert.Equal(88.5m, graded.Score);
        Assert.Equal(HttpStatusCode.Conflict, afterScore.StatusCode);
        Assert.Equal("assignments.already_graded", await afterScore.ProblemCodeAsync());
        Assert.Equal(88.5m, detail.MySubmission!.Score);
        Assert.Equal("Well explained", detail.MySubmission.Feedback);
        Assert.False(detail.CanEdit);
    }

    [Fact]
    public async Task A_file_of_another_student_or_type_is_refused()
    {
        var (scenario, assignment) = await GiveAsync();
        using var student = scenario.StudentClient(0);

        var otherKey = $"assignments/{assignment.Id}/submissions/{scenario.Students[1].Id}/x.pdf";
        fixture.Storage.Put(FileBucket.Private, otherKey, 100, "application/pdf");
        var foreign = await student.PutJsonAsync($"{Url}/{assignment.Id}/submission", new SubmitAssignmentCommand(assignment.Id, otherKey, "x.pdf", null));
        var word = await student.PostJsonAsync($"{Url}/{assignment.Id}/submission/upload-url", new CreateSubmissionUploadUrlCommand(assignment.Id, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 100));
        var empty = await student.PutJsonAsync($"{Url}/{assignment.Id}/submission", new SubmitAssignmentCommand(assignment.Id, null, null, "  "));

        Assert.Equal("files.invalid_key", await foreign.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, word.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Work_handed_in_after_the_due_date_is_marked_late()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var factory = fixture.CreateFactory(clock);
        var scenario = await ClassroomScenario.CreateAsync(factory, 1, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        var assignment = await (await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("Essay", null, clock.GetUtcNow().AddHours(1), scenario.Subject.Id, [scenario.Classroom.Id], null))).ReadAsync<AssignmentDto>();

        clock.Advance(TimeSpan.FromHours(2));
        using var student = scenario.StudentClient(0);
        var late = await SubmitTextAsync(student, assignment.Id);
        var list = await (await student.GetAsync($"{Url}/mine")).ReadAsync<PagedResult<StudentAssignmentDto>>();
        var rows = await (await teacher.GetAsync($"{Url}/{assignment.Id}/submissions")).ReadAsync<List<SubmissionRowDto>>();

        Assert.True(late.IsLate);
        Assert.Equal(StudentAssignmentStatus.Late, Assert.Single(list.Items).Status);
        Assert.Equal(StudentAssignmentStatus.Late, Assert.Single(rows).Status);
    }

    [Fact]
    public async Task An_assignment_not_handed_in_after_its_due_date_is_overdue()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var factory = fixture.CreateFactory(clock);
        var scenario = await ClassroomScenario.CreateAsync(factory, 1, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        (await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("Essay", null, clock.GetUtcNow().AddHours(1), scenario.Subject.Id, [scenario.Classroom.Id], null))).EnsureSuccessStatusCode();

        clock.Advance(TimeSpan.FromHours(2));
        using var student = scenario.StudentClient(0);
        var item = Assert.Single((await (await student.GetAsync($"{Url}/mine")).ReadAsync<PagedResult<StudentAssignmentDto>>()).Items);

        Assert.True(item.IsOverdue);
        Assert.Equal(StudentAssignmentStatus.NotSubmitted, item.Status);
    }

    [Fact]
    public async Task The_teacher_sees_every_student_handed_in_or_not_and_the_progress_in_the_list()
    {
        var (scenario, assignment) = await GiveAsync(students: 3);
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(1);
        var submission = await SubmitTextAsync(student, assignment.Id);
        (await teacher.PutJsonAsync($"{Url}/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, 70, null))).EnsureSuccessStatusCode();

        var rows = await (await teacher.GetAsync($"{Url}/{assignment.Id}/submissions")).ReadAsync<List<SubmissionRowDto>>();
        var list = await (await teacher.GetAsync($"{Url}?classroomId={scenario.Classroom.Id}")).ReadAsync<PagedResult<AssignmentListItemDto>>();
        var detail = await (await teacher.GetAsync($"{Url}/submissions/{submission.Id}")).ReadAsync<SubmissionDto>();

        Assert.Equal(3, rows.Count);
        Assert.Equal(2, rows.Count(r => r.Status == StudentAssignmentStatus.NotSubmitted));
        Assert.Equal(70, rows.Single(r => r.SubmissionId == submission.Id).Score);
        var item = Assert.Single(list.Items);
        Assert.Equal((3, 1, 1), (item.StudentCount, item.SubmittedCount, item.GradedCount));
        Assert.Equal([scenario.Classroom.Name], item.ClassroomNames);
        Assert.Equal("My answer", detail.TextBody);
    }

    [Fact]
    public async Task Scoring_notifies_the_student_and_a_score_stays_between_0_and_100()
    {
        var (scenario, assignment) = await GiveAsync(students: 1);
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(0);
        var submission = await SubmitTextAsync(student, assignment.Id);

        var tooHigh = await teacher.PutJsonAsync($"{Url}/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, 101, null));
        (await teacher.PutJsonAsync($"{Url}/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, 95, null))).EnsureSuccessStatusCode();
        var notifications = await (await student.GetAsync("/api/v1/notifications?pageSize=10")).ReadAsync<PagedResult<NotificationDto>>();

        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);
        Assert.Contains(notifications.Items, n => n.Title == "Your work was scored: Fractions" && n.Body == "Score 95");
    }

    [Fact]
    public async Task Only_the_teacher_who_gave_the_assignment_scores_and_changes_it()
    {
        var (scenario, assignment) = await GiveAsync(students: 1);
        using var student = scenario.StudentClient(0);
        var submission = await SubmitTextAsync(student, assignment.Id);
        using var otherTeacher = fixture.AsTeacher();

        var score = await otherTeacher.PutJsonAsync($"{Url}/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, 50, null));
        var update = await otherTeacher.PutJsonAsync($"{Url}/{assignment.Id}", new UpdateAssignmentCommand(assignment.Id, "Mine now", null, InTwoDays, [scenario.Classroom.Id], null, null, assignment.RowVersion));
        var delete = await otherTeacher.DeleteAsync($"{Url}/{assignment.Id}");
        var list = await otherTeacher.GetAsync($"{Url}/{assignment.Id}/submissions");

        Assert.Equal(HttpStatusCode.Forbidden, score.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task The_teacher_corrects_text_and_attachments_but_not_the_classrooms_once_work_is_in()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        var first = await UploadAttachmentAsync(teacher);
        var assignment = await (await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("Fractions", null, InTwoDays, scenario.Subject.Id, [scenario.Classroom.Id], [new NewAttachment(first, "old.pdf")]))).ReadAsync<AssignmentDto>();
        var second = await UploadAttachmentAsync(teacher, "image/png", 300);

        var updated = await (await teacher.PutJsonAsync($"{Url}/{assignment.Id}", new UpdateAssignmentCommand(
            assignment.Id, "Fractions II", "Now page 13", InTwoDays.AddDays(1), [scenario.Classroom.Id], [], [new NewAttachment(second, "new.png")], assignment.RowVersion))).ReadAsync<AssignmentDto>();
        using var student = scenario.StudentClient(0);
        await SubmitTextAsync(student, assignment.Id);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: DayOfWeek.Sunday);
        var moveClasses = await teacher.PutJsonAsync($"{Url}/{assignment.Id}", new UpdateAssignmentCommand(
            assignment.Id, "Fractions II", null, InTwoDays, [other.Classroom.Id], null, null, updated.RowVersion));
        var stale = await teacher.PutJsonAsync($"{Url}/{assignment.Id}", new UpdateAssignmentCommand(
            assignment.Id, "Stale", null, InTwoDays, [scenario.Classroom.Id], null, null, assignment.RowVersion));

        Assert.Equal("Fractions II", updated.Title);
        Assert.Equal("new.png", Assert.Single(updated.Attachments).FileName);
        Assert.False(fixture.Storage.Contains(FileBucket.Private, first)); // the removed attachment's file is deleted
        Assert.Equal("assignments.has_submissions", await moveClasses.ProblemCodeAsync());
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());
    }

    [Fact]
    public async Task An_assignment_with_scored_work_cannot_be_deleted_and_one_without_can()
    {
        var (scenario, scored) = await GiveAsync(students: 1);
        using var teacher = scenario.TeacherClient();
        using var student = scenario.StudentClient(0);
        var submission = await SubmitTextAsync(student, scored.Id);
        (await teacher.PutJsonAsync($"{Url}/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, 60, null))).EnsureSuccessStatusCode();
        var unscored = await (await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("Spare", null, InTwoDays, scenario.Subject.Id, [scenario.Classroom.Id], null))).ReadAsync<AssignmentDto>();

        var refused = await teacher.DeleteAsync($"{Url}/{scored.Id}");
        var deleted = await teacher.DeleteAsync($"{Url}/{unscored.Id}");

        Assert.Equal("assignments.has_scores", await refused.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"{Url}/{unscored.Id}")).StatusCode);
    }

    [Fact]
    public async Task Students_of_other_classrooms_and_admin_staff_cannot_open_an_assignment()
    {
        var (_, assignment) = await GiveAsync();
        using var stranger = fixture.AsStudent();
        using var admin = fixture.AsAdmin();
        using var principal = fixture.AsPrincipal();

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"{Url}/{assignment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"{Url}/{assignment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await principal.GetAsync($"{Url}/{assignment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PutJsonAsync($"{Url}/{assignment.Id}/submission", new SubmitAssignmentCommand(assignment.Id, null, null, "x"))).StatusCode);
    }

    [Fact]
    public async Task Attachments_and_handed_in_files_are_downloaded_through_short_lived_addresses()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        var key = await UploadAttachmentAsync(teacher);
        var assignment = await (await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("Read this", null, InTwoDays, scenario.Subject.Id, [scenario.Classroom.Id], [new NewAttachment(key, "text.pdf")]))).ReadAsync<AssignmentDto>();
        using var student = scenario.StudentClient(0);
        var upload = await (await student.PostJsonAsync($"{Url}/{assignment.Id}/submission/upload-url", new CreateSubmissionUploadUrlCommand(assignment.Id, "image/jpeg", 100))).ReadAsync<UploadUrlDto>();
        fixture.Storage.Put(FileBucket.Private, upload.Key, 100, "image/jpeg");
        var submission = await (await student.PutJsonAsync($"{Url}/{assignment.Id}/submission", new SubmitAssignmentCommand(assignment.Id, upload.Key, "photo.jpg", null))).ReadAsync<SubmissionDto>();
        using var stranger = fixture.AsStudent();

        var attachmentUrl = await (await student.GetAsync($"{Url}/{assignment.Id}/attachments/{assignment.Attachments[0].Id}/download-url")).ReadAsync<DownloadUrlDto>();
        var fileUrl = await (await teacher.GetAsync($"{Url}/submissions/{submission.Id}/download-url")).ReadAsync<DownloadUrlDto>();

        Assert.Contains(key, attachmentUrl.Url);
        Assert.Contains(upload.Key, fileUrl.Url);
        Assert.True(fileUrl.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"{Url}/submissions/{submission.Id}/download-url")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"{Url}/{assignment.Id}/attachments/{assignment.Attachments[0].Id}/download-url")).StatusCode);
    }

    [Fact]
    public async Task A_parent_follows_their_child_s_assignments_and_scores()
    {
        using var parent = fixture.AsParent();
        var children = await (await parent.GetAsync("/api/v1/users/me/children")).ReadAsync<List<ChildDto>>();
        var rina = children.Single(c => c.FullName == "Rina Putri");
        using var demoTeacher = fixture.AsTeacher(); // teaches Mathematics in Class A, Rina's classroom
        var classA = await TestData.ClassroomAsync(fixture.AsAdmin(), "Class A");
        var math = (await (await demoTeacher.GetAsync($"{Url}/teaching-options")).ReadAsync<List<TeachingOptionDto>>()).First(o => o.ClassroomId == classA.Id);
        var title = $"Parent view {Guid.NewGuid():N}"[..24];
        var assignment = await (await demoTeacher.PostJsonAsync(Url, new CreateAssignmentCommand(title, null, InTwoDays, math.SubjectId, [classA.Id], null))).ReadAsync<AssignmentDto>();
        using var student = fixture.AsStudent();
        var submission = await SubmitTextAsync(student, assignment.Id);
        (await demoTeacher.PutJsonAsync($"{Url}/submissions/{submission.Id}/score", new GradeSubmissionCommand(submission.Id, 77, null))).EnsureSuccessStatusCode();

        var list = await (await parent.GetAsync($"{Url}/children/{rina.StudentId}?pageSize=100")).ReadAsync<PagedResult<StudentAssignmentDto>>();
        var detail = await (await parent.GetAsync($"{Url}/{assignment.Id}?studentId={rina.StudentId}")).ReadAsync<AssignmentDto>();
        var stranger = await TestData.CreateStudentAsync(fixture.AsAdmin(), classA.Id);
        var notMine = await parent.GetAsync($"{Url}/children/{stranger.Id}");
        var noChild = await parent.GetAsync($"{Url}/{assignment.Id}");

        Assert.Equal(2, children.Count);
        Assert.Equal(77, list.Items.Single(a => a.Id == assignment.Id).Score);
        Assert.Equal(77, detail.MySubmission!.Score);
        Assert.Equal(HttpStatusCode.Forbidden, notMine.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, noChild.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    public async Task Only_parents_have_children(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users/me/children")).StatusCode);
    }

    [Fact]
    public async Task The_morning_reminder_goes_once_to_students_who_have_not_handed_in_work_due_tomorrow()
    {
        // 06:59 WIB on 28 October 2026; the reminder job runs at 07:00.
        var clock = new TestClock(TestClock.Wib(new DateOnly(2026, 10, 28), new TimeOnly(6, 59)));
        await using var factory = fixture.CreateFactory(clock);
        var scenario = await ClassroomScenario.CreateAsync(factory, 2, day: DayOfWeek.Saturday);
        using var teacher = scenario.TeacherClient();
        var tomorrow = await (await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("Due tomorrow", null, TestClock.Wib(new DateOnly(2026, 10, 29), new TimeOnly(23, 59)), scenario.Subject.Id, [scenario.Classroom.Id], null))).ReadAsync<AssignmentDto>();
        (await teacher.PostJsonAsync(Url, new CreateAssignmentCommand("Due next week", null, TestClock.Wib(new DateOnly(2026, 11, 4), new TimeOnly(23, 59)), scenario.Subject.Id, [scenario.Classroom.Id], null))).EnsureSuccessStatusCode();
        using var diligent = scenario.StudentClient(0);
        await SubmitTextAsync(diligent, tomorrow.Id);

        clock.Advance(TimeSpan.FromMinutes(1));
        int first, second;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<SendDueRemindersHandler>();
            first = await handler.Handle(CancellationToken.None);
            second = await handler.Handle(CancellationToken.None);
        }

        using var forgetful = scenario.StudentClient(1);
        var notes = await (await forgetful.GetAsync("/api/v1/notifications?pageSize=20")).ReadAsync<PagedResult<NotificationDto>>();
        var diligentNotes = await (await diligent.GetAsync("/api/v1/notifications?pageSize=20")).ReadAsync<PagedResult<NotificationDto>>();

        Assert.True(first >= 1);
        Assert.Equal(0, second); // a second run the same day sends nothing new
        var reminder = Assert.Single(notes.Items, n => n.Title.StartsWith("Due tomorrow", StringComparison.Ordinal) && n.Type == "AssignmentDueSoon");
        Assert.Contains("29 Oct 2026 23:59", reminder.Body);
        Assert.DoesNotContain(diligentNotes.Items, n => n.Type == "AssignmentDueSoon");
        Assert.DoesNotContain(notes.Items, n => n.Title.Contains("next week", StringComparison.Ordinal) && n.Type == "AssignmentDueSoon");
    }
}
