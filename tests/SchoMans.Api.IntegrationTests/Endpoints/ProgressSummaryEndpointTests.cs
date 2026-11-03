using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Notifications;
using SchoMans.Application.Features.ProgressSummaries;
using SchoMans.Application.Features.ProgressSummaries.DraftProgressSummaries;
using SchoMans.Application.Features.ProgressSummaries.EditProgressSummary;
using SchoMans.Application.Features.ProgressSummaries.RegenerateProgressSummary;
using SchoMans.Application.Features.ProgressSummaries.SendProgressSummaries;
using SchoMans.Application.Features.TeacherNotes;
using SchoMans.Application.Features.TeacherNotes.CreateTeacherNote;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>"Parent summaries sent after review" (AI-01): draft, review by the homeroom teacher, bulk send, parents' history.</summary>
[Collection(ApiCollection.Name)]
public class ProgressSummaryEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/progress-summaries";

    private static string LastMonth => ProgressSummary.MonthKey(TestClock.WibToday().AddMonths(-1));

    private static DateOnly DayOfLastMonth
    {
        get
        {
            var last = TestClock.WibToday().AddMonths(-1);
            return new DateOnly(last.Year, last.Month, 10);
        }
    }

    private static async Task NoteAsync(HttpClient teacher, Guid studentId, string body = "Helped a classmate with fractions.")
    {
        var response = await teacher.PostJsonAsync("/api/v1/teacher-notes", new CreateTeacherNoteCommand(studentId, body, DayOfLastMonth));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProgressSummaryDto> DraftAsync(HttpClient teacher, Guid studentId)
    {
        var response = await teacher.PostJsonAsync($"{Url}/regenerate", new RegenerateProgressSummaryCommand(studentId, LastMonth));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ProgressSummaryDto>();
    }

    private async Task<HttpClient> ParentOfAsync(Guid studentId)
    {
        var email = TestData.NextEmail("parent");
        var parent = fixture.AsUser(email, "Test Parent", $"sub-{email}", "Parent");
        (await parent.GetAsync("/api/v1/dashboard")).EnsureSuccessStatusCode(); // the first request creates the account
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parentId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        db.StudentParents.Add(StudentParent.Create(parentId, studentId));
        await db.SaveChangesAsync();
        return parent;
    }

    private static async Task<PagedResult<ProgressSummaryDto>> HistoryAsync(HttpClient parent, string query = "") =>
        await (await parent.GetAsync($"{Url}/mine{query}")).ReadAsync<PagedResult<ProgressSummaryDto>>();

    // ---- drafting ----

    [Fact]
    public async Task The_homeroom_teacher_gets_a_draft_written_from_the_notes_with_the_real_name_back_in_it()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        await NoteAsync(teacher, scenario.Students[0].Id);

        var draft = await DraftAsync(teacher, scenario.Students[0].Id);

        Assert.Equal(LastMonth, draft.Month);
        Assert.Equal("Draft", draft.Status);
        Assert.True(draft.IsAiDrafted);
        Assert.Contains("What is going well:", draft.Body, StringComparison.Ordinal);
        Assert.Contains("What needs attention:", draft.Body, StringComparison.Ordinal);
        Assert.Contains("A suggestion for home:", draft.Body, StringComparison.Ordinal);
        Assert.Contains(scenario.Students[0].FullName, draft.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("S1", draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asking_again_replaces_the_draft_instead_of_adding_a_second_summary()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        await NoteAsync(teacher, scenario.Students[0].Id);

        var first = await DraftAsync(teacher, scenario.Students[0].Id);
        var second = await DraftAsync(teacher, scenario.Students[0].Id);

        Assert.Equal(first.Id, second.Id);
        var list = await (await teacher.GetAsync($"{Url}?classroomId={scenario.Classroom.Id}&month={LastMonth}")).ReadAsync<List<ProgressSummaryDto>>();
        Assert.Single(list);
    }

    [Fact]
    public async Task A_student_with_no_data_that_month_gets_no_summary()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostJsonAsync($"{Url}/regenerate", new RegenerateProgressSummaryCommand(scenario.Students[0].Id, LastMonth));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("progress.no_data", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_month_that_has_not_started_is_refused()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var future = ProgressSummary.MonthKey(TestClock.WibToday().AddMonths(2));

        var response = await teacher.PostJsonAsync($"{Url}/regenerate", new RegenerateProgressSummaryCommand(scenario.Students[0].Id, future));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("progress.future_month", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task The_monthly_job_drafts_for_students_with_data_and_leaves_the_rest_and_runs_twice_safely()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 2);
        using var teacher = scenario.TeacherClient();
        await NoteAsync(teacher, scenario.Students[0].Id);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<DraftProgressSummariesHandler>();
        await job.Handle(CancellationToken.None);
        var again = await job.Handle(CancellationToken.None);

        var list = await (await teacher.GetAsync($"{Url}?classroomId={scenario.Classroom.Id}&month={LastMonth}")).ReadAsync<List<ProgressSummaryDto>>();
        var summary = Assert.Single(list);
        Assert.Equal(scenario.Students[0].Id, summary.StudentId);
        Assert.True(summary.IsAiDrafted);
        Assert.Equal(0, again);
    }

    [Fact]
    public async Task The_job_is_the_system_so_it_logs_its_calls_without_a_user()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        await NoteAsync(teacher, scenario.Students[0].Id);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DraftProgressSummariesHandler>().Handle(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AiUsageLogs.AnyAsync(l => l.Feature == "AI-01" && l.UserId == null && l.Succeeded));
    }

    // ---- review and send ----

    [Fact]
    public async Task An_edit_needs_a_new_approval_and_only_approved_summaries_are_sent()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 2);
        using var teacher = scenario.TeacherClient();
        await NoteAsync(teacher, scenario.Students[0].Id);
        await NoteAsync(teacher, scenario.Students[1].Id);
        var first = await DraftAsync(teacher, scenario.Students[0].Id);
        var second = await DraftAsync(teacher, scenario.Students[1].Id);

        var edited = await (await teacher.PutJsonAsync($"{Url}/{first.Id}", new EditProgressSummaryCommand(first.Id, "My own words for the parents."))).ReadAsync<ProgressSummaryDto>();
        var approved = await (await teacher.PostAsync($"{Url}/{first.Id}/approve", null)).ReadAsync<ProgressSummaryDto>();
        var sent = await (await teacher.PostJsonAsync($"{Url}/send", new SendProgressSummariesCommand(scenario.Classroom.Id, LastMonth))).ReadAsync<SentProgressSummariesDto>();

        Assert.Equal(("Draft", false), (edited.Status, edited.IsAiDrafted));
        Assert.Equal("Approved", approved.Status);
        Assert.Equal(1, sent.Sent);
        var list = await (await teacher.GetAsync($"{Url}?classroomId={scenario.Classroom.Id}&month={LastMonth}")).ReadAsync<List<ProgressSummaryDto>>();
        Assert.Equal("Sent", list.Single(s => s.Id == first.Id).Status);
        Assert.Equal("Draft", list.Single(s => s.Id == second.Id).Status);
    }

    [Fact]
    public async Task Sending_twice_sends_nothing_new_and_a_sent_summary_can_no_longer_change()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        await NoteAsync(teacher, scenario.Students[0].Id);
        var draft = await DraftAsync(teacher, scenario.Students[0].Id);
        await teacher.PostAsync($"{Url}/{draft.Id}/approve", null);
        var command = new SendProgressSummariesCommand(scenario.Classroom.Id, LastMonth);
        await teacher.PostJsonAsync($"{Url}/send", command);

        var again = await (await teacher.PostJsonAsync($"{Url}/send", command)).ReadAsync<SentProgressSummariesDto>();
        var edit = await teacher.PutJsonAsync($"{Url}/{draft.Id}", new EditProgressSummaryCommand(draft.Id, "Too late."));
        var approve = await teacher.PostAsync($"{Url}/{draft.Id}/approve", null);
        var regenerate = await teacher.PostJsonAsync($"{Url}/regenerate", new RegenerateProgressSummaryCommand(scenario.Students[0].Id, LastMonth));

        Assert.Equal(0, again.Sent);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        Assert.Equal("progress.already_sent", await regenerate.ProblemCodeAsync());
    }

    // ---- the family ----

    [Fact]
    public async Task Parents_see_nothing_until_the_summary_is_sent_then_they_are_notified_and_can_read_the_history()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        using var parent = await ParentOfAsync(scenario.Students[0].Id);
        await NoteAsync(teacher, scenario.Students[0].Id);
        var draft = await DraftAsync(teacher, scenario.Students[0].Id);

        var beforeApproval = await HistoryAsync(parent);
        await teacher.PostAsync($"{Url}/{draft.Id}/approve", null);
        var beforeSend = await HistoryAsync(parent);
        var sent = await (await teacher.PostJsonAsync($"{Url}/send", new SendProgressSummariesCommand(scenario.Classroom.Id, LastMonth))).ReadAsync<SentProgressSummariesDto>();
        var history = await HistoryAsync(parent, $"?studentId={scenario.Students[0].Id}");

        Assert.Empty(beforeApproval.Items);
        Assert.Empty(beforeSend.Items);
        Assert.Equal(1, sent.ParentsNotified);
        var summary = Assert.Single(history.Items);
        Assert.Equal(draft.Body, summary.Body);
        Assert.Equal("Sent", summary.Status);
        Assert.NotNull(summary.SentAt);

        var notifications = await (await parent.GetAsync("/api/v1/notifications?pageSize=100")).ReadAsync<PagedResult<NotificationDto>>();
        var notice = Assert.Single(notifications.Items, n => n.Type == "ProgressSummary");
        Assert.StartsWith("/progress-summaries", notice.LinkUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_parent_cannot_read_the_summaries_of_a_child_who_is_not_theirs()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 2);
        using var parent = await ParentOfAsync(scenario.Students[0].Id);

        var response = await parent.GetAsync($"{Url}/mine?studentId={scenario.Students[1].Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("progress.not_your_child", await response.ProblemCodeAsync());
    }

    // ---- validation and authorization ----

    [Fact]
    public async Task A_month_that_is_not_yyyy_MM_fails_validation()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var regenerate = await teacher.PostJsonAsync($"{Url}/regenerate", new RegenerateProgressSummaryCommand(scenario.Students[0].Id, "october"));
        var send = await teacher.PostJsonAsync($"{Url}/send", new SendProgressSummariesCommand(scenario.Classroom.Id, "2026-13"));
        var list = await teacher.GetAsync($"{Url}?classroomId={scenario.Classroom.Id}&month=oct");
        var edit = await teacher.PutJsonAsync($"{Url}/{Guid.NewGuid()}", new EditProgressSummaryCommand(Guid.NewGuid(), ""));
        var page = await fixture.AsParent().GetAsync($"{Url}/mine?pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, regenerate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, list.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
    }

    [Fact]
    public async Task Only_the_homeroom_teacher_of_the_classroom_may_work_with_its_summaries()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var homeroom = scenario.TeacherClient();
        await NoteAsync(homeroom, scenario.Students[0].Id);
        var draft = await DraftAsync(homeroom, scenario.Students[0].Id);
        using var stranger = other.TeacherClient();

        var responses = new[]
        {
            await stranger.GetAsync($"{Url}?classroomId={scenario.Classroom.Id}&month={LastMonth}"),
            await stranger.PostJsonAsync($"{Url}/regenerate", new RegenerateProgressSummaryCommand(scenario.Students[0].Id, LastMonth)),
            await stranger.PutJsonAsync($"{Url}/{draft.Id}", new EditProgressSummaryCommand(draft.Id, "Hijacked.")),
            await stranger.PostAsync($"{Url}/{draft.Id}/approve", null),
            await stranger.PostJsonAsync($"{Url}/send", new SendProgressSummariesCommand(scenario.Classroom.Id, LastMonth)),
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("progress.not_homeroom", await response.ProblemCodeAsync());
        }
    }

    [Fact]
    public async Task Other_roles_are_forbidden_by_policy()
    {
        var id = Guid.NewGuid();
        using var student = fixture.AsStudent();
        using var parent = fixture.AsParent();
        using var admin = fixture.AsAdmin();

        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"{Url}?classroomId={id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostJsonAsync($"{Url}/regenerate", new RegenerateProgressSummaryCommand(id, LastMonth))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"{Url}/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostJsonAsync($"{Url}/send", new SendProgressSummariesCommand(id, LastMonth))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"{Url}/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.AsTeacher().GetAsync($"{Url}/mine")).StatusCode);
    }

    [Fact]
    public async Task The_daily_ai_limit_of_a_teacher_stops_a_regeneration_with_a_clear_message()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        await NoteAsync(teacher, scenario.Students[0].Id);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var teacherUserId = await db.Users.Where(u => u.Email == scenario.Teacher.Email).Select(u => u.Id).SingleAsync();
            for (var i = 0; i < 10; i++)
            {
                db.AiUsageLogs.Add(SchoMans.Domain.Ai.AiUsageLog.Create(teacherUserId, "AI-01", "Mock", "mock", 10, 0m, 1, true));
            }

            await db.SaveChangesAsync();
        }

        var response = await teacher.PostJsonAsync($"{Url}/regenerate", new RegenerateProgressSummaryCommand(scenario.Students[0].Id, LastMonth));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("ai.daily_limit", await response.ProblemCodeAsync());
    }
}
