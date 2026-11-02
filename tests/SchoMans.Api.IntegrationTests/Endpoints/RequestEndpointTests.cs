using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Attendance;
using SchoMans.Application.Features.Letters;
using SchoMans.Application.Features.Notifications;
using SchoMans.Application.Features.Requests;
using SchoMans.Application.Features.Requests.CreateRequest;
using SchoMans.Application.Features.Requests.DecideRequest;
using SchoMans.Application.Features.Students.ListMyChildren;
using SchoMans.Domain.Approvals;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// "Approval flows pass": requests go through the chain of their type step by step, the right person decides, the requester is
/// told, and an approved certificate request issues a numbered letter. The demo accounts take the roles: the parent
/// (children Rina and Oka of Class A), the homeroom teacher of Class A, the admin staff and the principal.
/// </summary>
[Collection(ApiCollection.Name)]
public partial class RequestEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/requests";

    [GeneratedRegex(@"^\d{3,}/SK/SCHOMANS/(I|II|III|IV|V|VI|VII|VIII|IX|X|XI|XII)/\d{4}$")]
    private static partial Regex LetterNumber();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private async Task<(HttpClient Parent, ChildDto Rina, ChildDto Oka)> ParentAsync()
    {
        var parent = fixture.AsParent();
        var children = await (await parent.GetAsync("/api/v1/users/me/children")).ReadAsync<List<ChildDto>>();
        return (parent, children.Single(c => c.FullName == "Rina Putri"), children.First(c => c.FullName != "Rina Putri"));
    }

    private static CreateRequestCommand Leave(Guid studentId, string reason = "Fever") =>
        new(RequestType.StudentLeave, studentId, Today.AddDays(1), Today.AddDays(2), reason);

    private static CreateRequestCommand Letter(Guid studentId, string reason = "For a scholarship application") =>
        new(RequestType.CertificateLetter, studentId, null, null, reason);

    private static async Task<RequestDetailDto> CreatedAsync(HttpClient client, CreateRequestCommand command)
    {
        var response = await client.PostJsonAsync(Url, command);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<RequestDetailDto>();
    }

    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, Guid id, bool approve, string? note = null) =>
        client.PostJsonAsync($"{Url}/{id}/decision", new DecideRequestCommand(id, approve, note));

    private static async Task<bool> InInboxAsync(HttpClient client, Guid id)
    {
        var inbox = await (await client.GetAsync($"{Url}/inbox?pageSize=100")).ReadAsync<PagedResult<RequestListItemDto>>();
        return inbox.Items.Any(r => r.Id == id);
    }

    // ---- the chain, step by step ----

    [Fact]
    public async Task A_student_leave_goes_to_the_homeroom_teacher_and_then_the_principal()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;
        using var teacher = fixture.AsTeacher();
        using var principal = fixture.AsPrincipal();

        var request = await CreatedAsync(parent, Leave(rina.StudentId));
        Assert.Equal((RequestStatus.Pending, 1, ApproverRole.HomeroomTeacher), (request.Status, request.CurrentStep, request.WaitingFor));
        Assert.Equal([ApproverRole.HomeroomTeacher, ApproverRole.Principal], request.Chain);
        Assert.True(await InInboxAsync(teacher, request.Id));
        Assert.False(await InInboxAsync(principal, request.Id));

        var tooEarly = await DecideAsync(principal, request.Id, true);
        Assert.Equal((HttpStatusCode.Forbidden, "requests.not_your_turn"), (tooEarly.StatusCode, await tooEarly.ProblemCodeAsync()));

        var first = await (await DecideAsync(teacher, request.Id, true, "Get well soon")).ReadAsync<RequestDetailDto>();
        Assert.Equal((RequestStatus.Pending, 2, ApproverRole.Principal), (first.Status, first.CurrentStep, first.WaitingFor));
        Assert.False(await InInboxAsync(teacher, request.Id));
        Assert.True(await InInboxAsync(principal, request.Id));

        var again = await DecideAsync(teacher, request.Id, true);
        Assert.Equal("requests.not_your_turn", await again.ProblemCodeAsync());

        var done = await (await DecideAsync(principal, request.Id, true)).ReadAsync<RequestDetailDto>();
        Assert.Equal(RequestStatus.Approved, done.Status);
        Assert.Equal([ApproverRole.HomeroomTeacher, ApproverRole.Principal], done.Approvals.Select(a => a.Role));
        Assert.Equal("Get well soon", done.Approvals[0].Notes);
        Assert.Null(done.WaitingFor);
        Assert.False(done.CanDecide);

        var notices = await (await parent.GetAsync("/api/v1/notifications?pageSize=20")).ReadAsync<PagedResult<NotificationDto>>();
        Assert.Contains(notices.Items, n => n.Title == "Your request was approved" && n.LinkUrl == $"/requests/{request.Id}");
        var teacherNotices = await (await teacher.GetAsync("/api/v1/notifications?pageSize=50")).ReadAsync<PagedResult<NotificationDto>>();
        Assert.Contains(teacherNotices.Items, n => n.Title == "A request waits for your decision" && n.LinkUrl == $"/requests/{request.Id}");
    }

    [Fact]
    public async Task A_rejection_ends_the_request_with_the_note_and_nobody_decides_after_it()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;
        using var teacher = fixture.AsTeacher();
        using var principal = fixture.AsPrincipal();
        var request = await CreatedAsync(parent, Leave(rina.StudentId));

        var noNote = await DecideAsync(teacher, request.Id, false);
        var rejected = await (await DecideAsync(teacher, request.Id, false, "The exams are that week.")).ReadAsync<RequestDetailDto>();
        var late = await DecideAsync(principal, request.Id, true);
        var notices = await (await parent.GetAsync("/api/v1/notifications?pageSize=20")).ReadAsync<PagedResult<NotificationDto>>();

        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        Assert.Equal(RequestStatus.Rejected, rejected.Status);
        Assert.Equal("The exams are that week.", Assert.Single(rejected.Approvals).Notes);
        Assert.Equal((HttpStatusCode.Conflict, "requests.not_pending"), (late.StatusCode, await late.ProblemCodeAsync()));
        Assert.Contains(notices.Items, n => n.Title == "Your request was rejected" && n.Body == "The exams are that week.");
    }

    [Fact]
    public async Task A_teacher_s_leave_goes_to_the_principal_only()
    {
        using var teacher = fixture.AsTeacher();
        using var principal = fixture.AsPrincipal();
        using var admin = fixture.AsAdmin();

        var request = await CreatedAsync(teacher, new CreateRequestCommand(RequestType.TeacherLeave, null, Today.AddDays(3), Today.AddDays(4), "Training"));
        Assert.Equal([ApproverRole.Principal], request.Chain);
        Assert.True(await InInboxAsync(principal, request.Id));
        Assert.False(await InInboxAsync(admin, request.Id));

        var own = await DecideAsync(teacher, request.Id, true);
        var staff = await DecideAsync(admin, request.Id, true);
        var done = await (await DecideAsync(principal, request.Id, true)).ReadAsync<RequestDetailDto>();

        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);
        Assert.Equal(RequestStatus.Approved, done.Status);
    }

    // ---- the certificate letter ----

    [Fact]
    public async Task An_approved_certificate_request_issues_a_numbered_letter_that_can_be_downloaded_and_checked()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;
        using var admin = fixture.AsAdmin();
        using var principal = fixture.AsPrincipal();
        using var guest = fixture.Factory.CreateClient();

        var request = await CreatedAsync(parent, Letter(rina.StudentId));
        Assert.Equal([ApproverRole.Admin, ApproverRole.Principal], request.Chain);
        Assert.Null(request.StartDate);
        var adminStep = await (await DecideAsync(admin, request.Id, true)).ReadAsync<RequestDetailDto>();
        Assert.Null(adminStep.Letter);
        var done = await (await DecideAsync(principal, request.Id, true)).ReadAsync<RequestDetailDto>();

        var letter = Assert.IsType<LetterInfoDto>(done.Letter);
        Assert.Matches(LetterNumber(), letter.LetterNumber);
        Assert.True(fixture.Storage.Contains(FileBucket.Private, IssueLetterHandler.PdfKey(request.Id)));
        var info = await fixture.Storage.GetObjectInfoAsync(FileBucket.Private, IssueLetterHandler.PdfKey(request.Id), CancellationToken.None);
        Assert.Equal("application/pdf", info!.ContentType);
        Assert.True(info.SizeBytes > 2_000);

        var url = await (await parent.GetAsync($"/api/v1/letters/{request.Id}/url")).ReadAsync<LetterUrlDto>();
        Assert.Contains($"letters/{request.Id:N}.pdf", url.Url);
        Assert.Equal(letter.LetterNumber, url.LetterNumber);

        // The QR code points to https://school.test/verify/<code>; the code is in the letter and nowhere else.
        var code = await CodeOfAsync(request.Id);
        var check = await (await guest.GetAsync($"/api/v1/letters/verify/{code}")).ReadAsync<LetterVerificationDto>();
        Assert.Equal((true, letter.LetterNumber, "Rina Putri"), (check.Valid, check.LetterNumber, check.StudentName));
        Assert.NotNull(check.IssuedOn);

        var unknown = await (await guest.GetAsync("/api/v1/letters/verify/0123456789abcdef0123")).ReadAsync<LetterVerificationDto>();
        var junk = await (await guest.GetAsync("/api/v1/letters/verify/x")).ReadAsync<LetterVerificationDto>();
        Assert.False(unknown.Valid);
        Assert.False(junk.Valid);
        Assert.Null(unknown.StudentName);
    }

    [Fact]
    public async Task Letters_get_the_next_number_each_time()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;
        using var admin = fixture.AsAdmin();
        using var principal = fixture.AsPrincipal();
        var numbers = new List<string>();

        for (var i = 0; i < 2; i++)
        {
            var request = await CreatedAsync(parent, Letter(rina.StudentId, $"Purpose {i}"));
            await DecideAsync(admin, request.Id, true);
            numbers.Add((await (await DecideAsync(principal, request.Id, true)).ReadAsync<RequestDetailDto>()).Letter!.LetterNumber);
        }

        Assert.NotEqual(numbers[0], numbers[1]);
        Assert.True(int.Parse(numbers[1][..3]) == int.Parse(numbers[0][..3]) + 1);
    }

    [Fact]
    public async Task A_student_asks_for_a_letter_about_themselves_but_not_for_a_leave()
    {
        using var student = fixture.AsStudent();
        var me = await (await student.GetAsync("/api/v1/attendance/students/me?month=2026-10")).ReadAsync<StudentAttendanceDto>();

        var letter = await student.PostJsonAsync(Url, Letter(me.StudentId));
        var leave = await student.PostJsonAsync(Url, Leave(me.StudentId));
        var other = await student.PostJsonAsync(Url, Letter(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Created, letter.StatusCode);
        Assert.Equal((HttpStatusCode.Forbidden, "requests.not_allowed"), (leave.StatusCode, await leave.ProblemCodeAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, "requests.not_your_student"), (other.StatusCode, await other.ProblemCodeAsync()));
    }

    // ---- who may do what ----

    [Fact]
    public async Task A_parent_makes_requests_about_their_own_children_only_and_a_teacher_cannot_ask_for_a_student_leave()
    {
        var (parent, _, _) = await ParentAsync();
        using var parentScope = parent;
        using var teacher = fixture.AsTeacher();
        using var admin = fixture.AsAdmin();
        var stranger = await TestData.CreateStudentAsync(admin, (await TestData.ClassroomAsync(admin, "Class B")).Id);

        var foreign = await parent.PostJsonAsync(Url, Leave(stranger.Id));
        var teacherLeaveForStudent = await parent.PostJsonAsync(Url, new CreateRequestCommand(RequestType.TeacherLeave, null, Today, Today, "Mine"));
        var byTeacher = await teacher.PostJsonAsync(Url, Leave(stranger.Id));

        Assert.Equal((HttpStatusCode.Forbidden, "requests.not_your_student"), (foreign.StatusCode, await foreign.ProblemCodeAsync()));
        Assert.Equal("requests.not_allowed", await teacherLeaveForStudent.ProblemCodeAsync());
        Assert.Equal("requests.not_allowed", await byTeacher.ProblemCodeAsync());
    }

    [Fact]
    public async Task Only_the_homeroom_teacher_of_the_student_decides_the_first_step()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: DayOfWeek.Sunday);
        using var stranger = other.TeacherClient();
        var request = await CreatedAsync(parent, Leave(rina.StudentId));

        var decision = await DecideAsync(stranger, request.Id, true);

        Assert.Equal((HttpStatusCode.Forbidden, "requests.not_your_turn"), (decision.StatusCode, await decision.ProblemCodeAsync()));
        Assert.False(await InInboxAsync(stranger, request.Id));
    }

    [Fact]
    public async Task The_requester_withdraws_a_pending_request_and_nobody_else_does()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;
        using var teacher = fixture.AsTeacher();
        var request = await CreatedAsync(parent, Leave(rina.StudentId));

        var byTeacher = await teacher.PostAsync($"{Url}/{request.Id}/cancel", null);
        var cancelled = await (await parent.PostAsync($"{Url}/{request.Id}/cancel", null)).ReadAsync<RequestDetailDto>();
        var again = await parent.PostAsync($"{Url}/{request.Id}/cancel", null);
        var decide = await DecideAsync(teacher, request.Id, true);

        Assert.Equal(HttpStatusCode.Forbidden, byTeacher.StatusCode);
        Assert.Equal(RequestStatus.Cancelled, cancelled.Status);
        Assert.False(cancelled.CanCancel);
        Assert.Equal((HttpStatusCode.Conflict, "requests.not_pending"), (again.StatusCode, await again.ProblemCodeAsync()));
        Assert.Equal("requests.not_pending", await decide.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_request_is_seen_by_its_people_and_not_by_strangers()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;
        var request = await CreatedAsync(parent, Leave(rina.StudentId));
        using var student = fixture.AsStudent(); // Rina herself
        using var teacher = fixture.AsTeacher(); // homeroom teacher of Class A
        using var principal = fixture.AsPrincipal();
        using var admin = fixture.AsAdmin();
        using var stranger = fixture.AsNewUser("Parent");
        var otherClass = await ClassroomScenario.CreateAsync(fixture.Factory, 0, day: DayOfWeek.Sunday);
        using var otherTeacher = otherClass.TeacherClient();

        foreach (var client in new[] { parent, student, teacher, principal, admin })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Url}/{request.Id}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"{Url}/{request.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherTeacher.GetAsync($"{Url}/{request.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await parent.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/v1/letters/{request.Id}/url")).StatusCode);
    }

    [Fact]
    public async Task My_requests_include_what_was_made_about_my_child_and_the_staff_list_everything()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;
        using var student = fixture.AsStudent();
        using var principal = fixture.AsPrincipal();
        var request = await CreatedAsync(parent, Letter(rina.StudentId, "A letter about Rina"));

        var mine = await (await parent.GetAsync($"{Url}/mine?pageSize=100")).ReadAsync<PagedResult<RequestListItemDto>>();
        var hers = await (await student.GetAsync($"{Url}/mine?pageSize=100")).ReadAsync<PagedResult<RequestListItemDto>>();
        var all = await (await principal.GetAsync($"{Url}?type=CertificateLetter&status=Pending&pageSize=100")).ReadAsync<PagedResult<RequestListItemDto>>();
        var forbidden = await parent.GetAsync(Url);

        var row = Assert.Single(mine.Items, r => r.Id == request.Id);
        Assert.Equal(("Rina Putri", RequestType.CertificateLetter, 2), (row.StudentName, row.Type, row.TotalSteps));
        Assert.Contains(hers.Items, r => r.Id == request.Id); // it is about her
        Assert.Contains(all.Items, r => r.Id == request.Id);
        Assert.All(all.Items, r => Assert.Equal((RequestType.CertificateLetter, RequestStatus.Pending), (r.Type, r.Status)));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    // ---- validation ----

    [Fact]
    public async Task A_request_needs_a_reason_dates_in_order_and_a_start_that_is_not_long_past()
    {
        var (parent, rina, _) = await ParentAsync();
        using var parentScope = parent;

        var noReason = await parent.PostJsonAsync(Url, Leave(rina.StudentId, " "));
        var noDates = await parent.PostJsonAsync(Url, new CreateRequestCommand(RequestType.StudentLeave, rina.StudentId, null, null, "Fever"));
        var reversed = await parent.PostJsonAsync(Url, new CreateRequestCommand(RequestType.StudentLeave, rina.StudentId, Today.AddDays(3), Today.AddDays(1), "Fever"));
        var tooLong = await parent.PostJsonAsync(Url, new CreateRequestCommand(RequestType.StudentLeave, rina.StudentId, Today, Today.AddDays(90), "Fever"));
        var past = await parent.PostJsonAsync(Url, new CreateRequestCommand(RequestType.StudentLeave, rina.StudentId, Today.AddDays(-30), Today.AddDays(-29), "Fever"));
        var noStudent = await parent.PostJsonAsync(Url, new CreateRequestCommand(RequestType.CertificateLetter, null, null, null, "Letter"));

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noDates.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("requests.too_early", await past.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noStudent.StatusCode);
    }

    // ---- roles ----

    [Theory]
    [InlineData("Applicant")]
    public async Task Applicants_have_no_requests(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, Letter(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/letters/{Guid.NewGuid()}/url")).StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    public async Task Families_have_no_inbox_and_do_not_decide(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/inbox")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(client, Guid.NewGuid(), true)).StatusCode);
    }

    [Fact]
    public async Task Requests_need_a_token_but_the_letter_check_does_not()
    {
        using var guest = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"{Url}/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"/api/v1/letters/{Guid.NewGuid()}/url")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/v1/letters/verify/0123456789abcdef0123")).StatusCode);
    }

    private async Task<string> CodeOfAsync(Guid requestId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Letters.Where(l => l.RequestId == requestId).Select(l => l.VerificationCode).SingleAsync();
    }
}
