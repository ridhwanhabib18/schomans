using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Announcements;
using SchoMans.Application.Features.Announcements.CreateAnnouncement;
using SchoMans.Application.Features.Announcements.PublishAnnouncement;
using SchoMans.Application.Features.Announcements.UpdateAnnouncement;
using SchoMans.Application.Features.Classrooms;
using SchoMans.Application.Features.Classrooms.CreateClassroom;
using SchoMans.Application.Features.Files.CreateUploadUrl;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class AnnouncementEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/announcements";

    private static string UniqueTitle(string prefix = "Notice") => $"{prefix} {Guid.NewGuid():N}";

    private static async Task<AnnouncementDto> CreateAsync(
        HttpClient editor,
        string? title = null,
        bool isPublic = false,
        UserRole[]? roles = null,
        Guid[]? classrooms = null,
        string body = "<p>Please read.</p>")
    {
        var response = await editor.PostJsonAsync(Url, new CreateAnnouncementCommand(title ?? UniqueTitle(), body, isPublic, roles ?? [], classrooms ?? []));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<AnnouncementDto>();
    }

    private static async Task<PublishAnnouncementResult> PublishAsync(HttpClient editor, Guid id)
    {
        var response = await editor.PostAsync($"{Url}/{id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PublishAnnouncementResult>();
    }

    private static async Task<List<AnnouncementSummaryDto>> InboxAsync(HttpClient client, string query = "") =>
        (await (await client.GetAsync($"{Url}/mine?pageSize=100{query}")).ReadAsync<PagedResult<AnnouncementSummaryDto>>()).Items.ToList();

    private async Task<List<Guid>> NotifiedUsersAsync(Guid announcementId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = $"/announcements/{announcementId}";
        return await db.Notifications.Where(n => n.LinkUrl == link).Select(n => n.UserId).ToListAsync();
    }

    private async Task<Guid> UserIdAsync(string email)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    /// <summary>A classroom of its own with 3 students, one parent (the demo parent) and its own homeroom teacher.</summary>
    private sealed record Scenario(ClassroomDto Classroom, string TeacherEmail, string[] StudentEmails);

    private async Task<Scenario> CreateClassroomScenarioAsync()
    {
        using var admin = fixture.AsAdmin();
        var year = (await (await admin.GetAsync("/api/v1/academic-years")).ReadAsync<PagedResult<SchoMans.Application.Features.AcademicYears.AcademicYearDto>>()).Items.Single(y => y.IsActive);
        var teacher = await TestData.CreateTeacherAsync(admin);
        var classroom = await (await admin.PostJsonAsync("/api/v1/classrooms", new CreateClassroomCommand($"Scn {Guid.NewGuid():N}"[..20], year.Id, teacher.Id))).ReadAsync<ClassroomDto>();
        var students = new List<StudentSummary>();
        for (var i = 0; i < 3; i++)
        {
            var student = await TestData.CreateStudentAsync(admin, classroom.Id);
            students.Add(new StudentSummary(student.Id, student.Email));
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parentId = await db.Users.Where(u => u.Email == "parent@schomans.demo").Select(u => u.Id).SingleAsync();
        db.StudentParents.Add(StudentParent.Create(parentId, students[0].Id)); // one parent, one child in this classroom
        await db.SaveChangesAsync();
        return new Scenario(classroom, teacher.Email, students.Select(s => s.Email).ToArray());
    }

    private sealed record StudentSummary(Guid Id, string Email);

    // ---- writing ----

    [Fact]
    public async Task An_editor_creates_a_draft_announcement()
    {
        using var admin = fixture.AsAdmin();

        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher, UserRole.Parent]);

        Assert.Equal("Draft", announcement.Status);
        Assert.Null(announcement.PublishedAt);
        Assert.Equal(["Teacher", "Parent"], announcement.TargetRoles);
        Assert.False(announcement.IsPublic);
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Applicant")]
    public async Task Other_roles_may_not_write_announcements(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, new CreateAnnouncementCommand("T", "<p>x</p>", false, [], []))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Url}/{Guid.NewGuid()}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task An_invalid_announcement_is_rejected()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateAnnouncementCommand("", "", false, [(UserRole)99], [Guid.Empty]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_target_classroom_must_exist()
    {
        using var admin = fixture.AsAdmin();

        var response = await admin.PostJsonAsync(Url, new CreateAnnouncementCommand("T", "<p>x</p>", false, [], [Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("classrooms.not_found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task The_body_is_sanitized()
    {
        using var admin = fixture.AsAdmin();

        var announcement = await CreateAsync(admin, body: "<p>Hi</p><script>alert(1)</script><img src=x onerror=alert(2)>");

        Assert.Equal("<p>Hi</p>", announcement.Body);
    }

    [Fact]
    public async Task The_audience_can_change_while_a_draft_but_is_locked_after_publishing()
    {
        using var admin = fixture.AsAdmin();
        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher]);
        UpdateAnnouncementCommand Edit(AnnouncementDto a, string title, UserRole[] roles, bool isPublic = false) =>
            new(a.Id, title, a.Body, isPublic, roles, [], a.CoverImageKey, a.RowVersion);

        var retarget = await admin.PutJsonAsync($"{Url}/{announcement.Id}", Edit(announcement, "Retargeted", [UserRole.Parent]));
        Assert.Equal(HttpStatusCode.OK, retarget.StatusCode);
        var draft = await retarget.ReadAsync<AnnouncementDto>();
        Assert.Equal(["Parent"], draft.TargetRoles);

        await PublishAsync(admin, announcement.Id);
        var published = await (await admin.GetAsync($"{Url}/{announcement.Id}")).ReadAsync<AnnouncementDto>();

        var lockedAudience = await admin.PutJsonAsync($"{Url}/{announcement.Id}", Edit(published, "Late retarget", [UserRole.Student]));
        var fixedTypo = await admin.PutJsonAsync($"{Url}/{announcement.Id}", Edit(published, "Corrected title", [UserRole.Parent]));

        Assert.Equal(HttpStatusCode.Conflict, lockedAudience.StatusCode);
        Assert.Equal("announcements.audience_locked", await lockedAudience.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, fixedTypo.StatusCode);
        Assert.Equal("Corrected title", (await fixedTypo.ReadAsync<AnnouncementDto>()).Title);
    }

    [Fact]
    public async Task A_stale_update_is_rejected()
    {
        using var admin = fixture.AsAdmin();
        var announcement = await CreateAsync(admin);
        UpdateAnnouncementCommand Edit(string title) => new(announcement.Id, title, announcement.Body, false, [], [], null, announcement.RowVersion);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutJsonAsync($"{Url}/{announcement.Id}", Edit("First"))).StatusCode);
        var stale = await admin.PutJsonAsync($"{Url}/{announcement.Id}", Edit("Second"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await stale.ProblemCodeAsync());
    }

    [Fact]
    public async Task Unknown_announcements_are_not_found_for_every_action()
    {
        using var admin = fixture.AsAdmin();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutJsonAsync($"{Url}/{id}", new UpdateAnnouncementCommand(id, "T", "<p>x</p>", false, [], [], null, "AAAAAAAAB9E="))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"{Url}/{id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Url}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/mine/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"{Url}/mine/{id}/read", null)).StatusCode);
    }

    [Fact]
    public async Task The_editors_list_can_be_filtered_by_status_and_title()
    {
        using var admin = fixture.AsAdmin();
        var marker = Guid.NewGuid().ToString("N");
        var draft = await CreateAsync(admin, $"Draft {marker}");
        var published = await CreateAsync(admin, $"Published {marker}");
        await PublishAsync(admin, published.Id);

        var all = await (await admin.GetAsync($"{Url}?search={marker}")).ReadAsync<PagedResult<AnnouncementDto>>();
        var drafts = await (await admin.GetAsync($"{Url}?search={marker}&status=Draft")).ReadAsync<PagedResult<AnnouncementDto>>();

        Assert.Equal(2, all.TotalCount);
        Assert.Equal([draft.Id], drafts.Items.Select(a => a.Id));
    }

    // ---- publishing and who is notified ----

    [Fact]
    public async Task Publishing_twice_is_a_conflict()
    {
        using var admin = fixture.AsAdmin();
        var announcement = await CreateAsync(admin, roles: [UserRole.Applicant]);
        await PublishAsync(admin, announcement.Id);

        var again = await admin.PostAsync($"{Url}/{announcement.Id}/publish", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("announcements.already_published", await again.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_classroom_targeted_announcement_notifies_its_students_their_parents_and_the_homeroom_teacher_only()
    {
        var scenario = await CreateClassroomScenarioAsync();
        using var admin = fixture.AsAdmin();
        var announcement = await CreateAsync(admin, classrooms: [scenario.Classroom.Id]);

        var result = await PublishAsync(admin, announcement.Id);
        var notified = await NotifiedUsersAsync(announcement.Id);

        Assert.Equal(5, result.NotifiedUsers); // 3 students + 1 parent + 1 homeroom teacher
        Assert.Equal(5, notified.Count);
        foreach (var email in scenario.StudentEmails.Append(scenario.TeacherEmail).Append("parent@schomans.demo"))
        {
            Assert.Contains(await UserIdAsync(email), notified);
        }

        // Nobody else: not a student of another classroom, not another teacher, not the admin.
        foreach (var email in new[] { "student@schomans.demo", "teacher@schomans.demo", "admin@schomans.demo", "principal@schomans.demo", "applicant@schomans.demo" })
        {
            Assert.DoesNotContain(await UserIdAsync(email), notified);
        }
    }

    [Fact]
    public async Task Roles_and_classrooms_narrow_each_other()
    {
        var scenario = await CreateClassroomScenarioAsync();
        using var admin = fixture.AsAdmin();
        var announcement = await CreateAsync(admin, roles: [UserRole.Parent], classrooms: [scenario.Classroom.Id]);

        var result = await PublishAsync(admin, announcement.Id);

        Assert.Equal(1, result.NotifiedUsers);
        Assert.Equal([await UserIdAsync("parent@schomans.demo")], await NotifiedUsersAsync(announcement.Id));
    }

    [Fact]
    public async Task A_role_targeted_announcement_notifies_everybody_with_that_role()
    {
        using var admin = fixture.AsAdmin();
        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher]);

        var result = await PublishAsync(admin, announcement.Id);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var teachers = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(u => u.Role == UserRole.Teacher);
        Assert.Equal(teachers, result.NotifiedUsers);
        Assert.True(teachers >= 15);
    }

    [Fact]
    public async Task An_announcement_without_targets_reaches_every_school_role_but_not_applicants()
    {
        using var admin = fixture.AsAdmin();
        var announcement = await CreateAsync(admin);

        await PublishAsync(admin, announcement.Id);
        var notified = await NotifiedUsersAsync(announcement.Id);

        foreach (var email in new[] { "admin@schomans.demo", "principal@schomans.demo", "teacher@schomans.demo", "student@schomans.demo", "parent@schomans.demo" })
        {
            Assert.Contains(await UserIdAsync(email), notified);
        }

        Assert.DoesNotContain(await UserIdAsync("applicant@schomans.demo"), notified);
    }

    // ---- reading ----

    [Fact]
    public async Task A_draft_is_in_nobodys_inbox_and_a_published_one_only_in_the_inbox_of_its_audience()
    {
        using var admin = fixture.AsAdmin();
        using var teacher = fixture.AsTeacher();
        using var student = fixture.AsStudent();
        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher]);

        Assert.DoesNotContain(await InboxAsync(teacher), a => a.Id == announcement.Id);

        await PublishAsync(admin, announcement.Id);

        Assert.Contains(await InboxAsync(teacher), a => a.Id == announcement.Id);
        Assert.DoesNotContain(await InboxAsync(student), a => a.Id == announcement.Id);
    }

    [Fact]
    public async Task Admin_and_principal_read_every_published_announcement_whoever_it_is_for()
    {
        using var admin = fixture.AsAdmin();
        using var principal = fixture.AsPrincipal();
        var announcement = await CreateAsync(admin, roles: [UserRole.Parent]);
        await PublishAsync(admin, announcement.Id);

        Assert.Contains(await InboxAsync(admin), a => a.Id == announcement.Id);
        Assert.Contains(await InboxAsync(principal), a => a.Id == announcement.Id);
    }

    [Fact]
    public async Task A_parent_reads_the_announcements_of_the_classrooms_of_their_children()
    {
        var scenario = await CreateClassroomScenarioAsync();
        using var admin = fixture.AsAdmin();
        using var parent = fixture.AsParent();
        using var student = fixture.AsStudent();
        var announcement = await CreateAsync(admin, classrooms: [scenario.Classroom.Id]);
        await PublishAsync(admin, announcement.Id);

        Assert.Contains(await InboxAsync(parent), a => a.Id == announcement.Id);
        Assert.DoesNotContain(await InboxAsync(student), a => a.Id == announcement.Id); // a student of another classroom
    }

    [Fact]
    public async Task A_student_of_the_classroom_and_its_homeroom_teacher_read_it()
    {
        var scenario = await CreateClassroomScenarioAsync();
        using var admin = fixture.AsAdmin();
        var announcement = await CreateAsync(admin, classrooms: [scenario.Classroom.Id]);
        await PublishAsync(admin, announcement.Id);
        var email = scenario.StudentEmails[1];
        using var student = fixture.AsUser(email, "Classroom Student", $"sub-{Guid.NewGuid():N}", "Student");
        using var homeroom = fixture.AsUser(scenario.TeacherEmail, "Homeroom Teacher", $"sub-{Guid.NewGuid():N}", "Teacher");

        Assert.Contains(await InboxAsync(student), a => a.Id == announcement.Id);
        Assert.Contains(await InboxAsync(homeroom), a => a.Id == announcement.Id);
    }

    [Fact]
    public async Task An_applicant_only_reads_announcements_that_name_the_applicant_role()
    {
        using var admin = fixture.AsAdmin();
        using var applicant = fixture.AsApplicant();
        var everyone = await CreateAsync(admin);
        var forApplicants = await CreateAsync(admin, roles: [UserRole.Applicant]);
        await PublishAsync(admin, everyone.Id);
        await PublishAsync(admin, forApplicants.Id);

        var inbox = await InboxAsync(applicant);

        Assert.DoesNotContain(inbox, a => a.Id == everyone.Id);
        Assert.Contains(inbox, a => a.Id == forApplicants.Id);
    }

    [Fact]
    public async Task The_inbox_is_newest_first_pageable_and_can_show_unread_only()
    {
        using var admin = fixture.AsAdmin();
        using var teacher = fixture.AsTeacher();
        var first = await CreateAsync(admin, UniqueTitle("First"), roles: [UserRole.Teacher]);
        await PublishAsync(admin, first.Id);
        await Task.Delay(30);
        var second = await CreateAsync(admin, UniqueTitle("Second"), roles: [UserRole.Teacher]);
        await PublishAsync(admin, second.Id);
        (await teacher.PostAsync($"{Url}/mine/{first.Id}/read", null)).EnsureSuccessStatusCode();

        var inbox = await InboxAsync(teacher);
        var unread = await InboxAsync(teacher, "&unreadOnly=true");
        var onePerPage = await (await teacher.GetAsync($"{Url}/mine?page=1&pageSize=1")).ReadAsync<PagedResult<AnnouncementSummaryDto>>();

        Assert.True(inbox.FindIndex(a => a.Id == second.Id) < inbox.FindIndex(a => a.Id == first.Id));
        Assert.True(inbox.Single(a => a.Id == first.Id).IsRead);
        Assert.False(inbox.Single(a => a.Id == second.Id).IsRead);
        Assert.DoesNotContain(unread, a => a.Id == first.Id);
        Assert.Contains(unread, a => a.Id == second.Id);
        Assert.Single(onePerPage.Items);
        Assert.True(onePerPage.TotalCount >= 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.GetAsync($"{Url}/mine?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task One_announcement_is_readable_by_its_audience_and_not_found_for_anybody_else()
    {
        using var admin = fixture.AsAdmin();
        using var teacher = fixture.AsTeacher();
        using var student = fixture.AsStudent();
        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher], body: "<p>Teachers only</p>");
        await PublishAsync(admin, announcement.Id);

        var mine = await teacher.GetAsync($"{Url}/mine/{announcement.Id}");
        var notMine = await student.GetAsync($"{Url}/mine/{announcement.Id}");

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal("<p>Teachers only</p>", (await mine.ReadAsync<AnnouncementReaderDto>()).Body);
        Assert.Equal(HttpStatusCode.NotFound, notMine.StatusCode);
    }

    [Fact]
    public async Task A_draft_cannot_be_read_through_the_inbox_even_by_its_author()
    {
        using var admin = fixture.AsAdmin();
        var draft = await CreateAsync(admin);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/mine/{draft.Id}")).StatusCode);
    }

    [Fact]
    public async Task Reading_marks_it_read_once_and_clears_its_notification()
    {
        using var admin = fixture.AsAdmin();
        using var teacher = fixture.AsTeacher();
        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher]);
        await PublishAsync(admin, announcement.Id);
        var before = await (await teacher.GetAsync("/api/v1/notifications/unread-count")).ReadAsync<SchoMans.Application.Features.Notifications.GetUnreadNotificationCount.UnreadNotificationCountDto>();

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync($"{Url}/mine/{announcement.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync($"{Url}/mine/{announcement.Id}/read", null)).StatusCode); // idempotent

        var after = await (await teacher.GetAsync("/api/v1/notifications/unread-count")).ReadAsync<SchoMans.Application.Features.Notifications.GetUnreadNotificationCount.UnreadNotificationCountDto>();
        Assert.Equal(before.Count - 1, after.Count);
        Assert.True((await (await teacher.GetAsync($"{Url}/mine/{announcement.Id}")).ReadAsync<AnnouncementReaderDto>()).IsRead);
    }

    [Fact]
    public async Task Somebody_outside_the_audience_cannot_mark_it_read()
    {
        using var admin = fixture.AsAdmin();
        using var student = fixture.AsStudent();
        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher]);
        await PublishAsync(admin, announcement.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await student.PostAsync($"{Url}/mine/{announcement.Id}/read", null)).StatusCode);
    }

    // ---- guests ----

    [Fact]
    public async Task Guests_see_only_published_announcements_marked_public()
    {
        using var admin = fixture.AsAdmin();
        using var guest = fixture.Factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N");
        var publicOne = await CreateAsync(admin, $"Public {marker}", isPublic: true);
        var internalOne = await CreateAsync(admin, $"Internal {marker}", isPublic: false);
        var publicDraft = await CreateAsync(admin, $"Public draft {marker}", isPublic: true);
        await PublishAsync(admin, publicOne.Id);
        await PublishAsync(admin, internalOne.Id);

        var list = await (await guest.GetAsync($"{Url}/public?pageSize=100")).ReadAsync<PagedResult<AnnouncementSummaryDto>>();

        Assert.Contains(list.Items, a => a.Id == publicOne.Id);
        Assert.DoesNotContain(list.Items, a => a.Id == internalOne.Id);
        Assert.DoesNotContain(list.Items, a => a.Id == publicDraft.Id);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"{Url}/public/{publicOne.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Url}/public/{internalOne.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Url}/public/{publicDraft.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_inbox_needs_a_signed_in_user()
    {
        using var guest = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"{Url}/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(Url)).StatusCode);
    }

    // ---- deleting ----

    [Fact]
    public async Task Deleting_an_announcement_removes_it_from_inboxes_and_removes_its_notifications()
    {
        using var admin = fixture.AsAdmin();
        using var teacher = fixture.AsTeacher();
        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher]);
        await PublishAsync(admin, announcement.Id);
        Assert.NotEmpty(await NotifiedUsersAsync(announcement.Id));

        var deleted = await admin.DeleteAsync($"{Url}/{announcement.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.DoesNotContain(await InboxAsync(teacher), a => a.Id == announcement.Id);
        Assert.Empty(await NotifiedUsersAsync(announcement.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{announcement.Id}")).StatusCode);
    }

    // ---- cover images (private bucket, presigned download) ----

    [Fact]
    public async Task An_announcement_cover_goes_to_the_private_bucket_and_readers_get_a_presigned_link()
    {
        using var admin = fixture.AsAdmin();
        using var teacher = fixture.AsTeacher();
        var announcement = await CreateAsync(admin, roles: [UserRole.Teacher]);

        var upload = await (await admin.PostJsonAsync("/api/v1/files/upload-url", new CreateUploadUrlCommand("announcements", announcement.Id, "image/webp", 4096))).ReadAsync<UploadUrlDto>();
        Assert.StartsWith("announcements/2026-2027/", upload.Key, StringComparison.Ordinal);
        Assert.Contains($"/{announcement.Id}/", upload.Key, StringComparison.Ordinal);
        Assert.Contains("/Private/", upload.Url, StringComparison.Ordinal);

        fixture.Storage.Put(FileBucket.Private, upload.Key, 4096, "image/webp");
        var saved = await admin.PutJsonAsync($"{Url}/{announcement.Id}", new UpdateAnnouncementCommand(
            announcement.Id, announcement.Title, announcement.Body, false, [UserRole.Teacher], [], upload.Key, announcement.RowVersion));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        await PublishAsync(admin, announcement.Id);

        var reader = await (await teacher.GetAsync($"{Url}/mine/{announcement.Id}")).ReadAsync<AnnouncementReaderDto>();

        Assert.Equal($"https://storage.test/private/{upload.Key}?signature=fake", reader.CoverImageUrl);
    }
}
