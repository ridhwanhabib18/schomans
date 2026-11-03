using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.TeacherNotes;
using SchoMans.Application.Features.TeacherNotes.CreateTeacherNote;
using SchoMans.Domain.Students;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>Teacher notes: the teachers of a classroom keep short notes about its students; families never see them.</summary>
[Collection(ApiCollection.Name)]
public class TeacherNoteEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/teacher-notes";

    [Fact]
    public async Task A_teacher_writes_a_note_and_it_is_listed_newest_day_first_with_a_delete_flag_for_the_author()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var student = scenario.Students[0].Id;
        var today = TestClock.WibToday();

        var older = await teacher.PostJsonAsync(Url, new CreateTeacherNoteCommand(student, "Older note", today.AddDays(-3)));
        var newer = await teacher.PostJsonAsync(Url, new CreateTeacherNoteCommand(student, "Newer note"));

        Assert.Equal(HttpStatusCode.OK, older.StatusCode);
        var created = await newer.ReadAsync<TeacherNoteDto>();
        Assert.Equal(today, created.Date);
        Assert.True(created.IsMine);
        var page = await (await teacher.GetAsync($"{Url}?studentId={student}")).ReadAsync<PagedResult<TeacherNoteDto>>();
        Assert.Equal(["Newer note", "Older note"], page.Items.Select(n => n.Body));
        Assert.All(page.Items, n => Assert.True(n.IsMine));
    }

    [Fact]
    public async Task Only_the_author_deletes_a_note()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var author = scenario.TeacherClient();
        var note = await (await author.PostJsonAsync(Url, new CreateTeacherNoteCommand(scenario.Students[0].Id, "Mine"))).ReadAsync<TeacherNoteDto>();

        // The demo teacher also works in classrooms of the demo school, but not in this one: not even a reader.
        var otherTeacher = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var stranger = otherTeacher.TeacherClient();
        var refused = await stranger.DeleteAsync($"{Url}/{note.Id}");
        var deleted = await author.DeleteAsync($"{Url}/{note.Id}");
        var again = await author.DeleteAsync($"{Url}/{note.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("notes.not_yours", await refused.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task A_teacher_only_works_with_notes_of_students_in_their_own_classrooms()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var stranger = other.TeacherClient();

        var write = await stranger.PostJsonAsync(Url, new CreateTeacherNoteCommand(scenario.Students[0].Id, "Not mine to write"));
        var read = await stranger.GetAsync($"{Url}?studentId={scenario.Students[0].Id}");

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal("notes.not_your_student", await write.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task An_unknown_student_is_not_found()
    {
        using var teacher = fixture.AsTeacher();

        var response = await teacher.PostJsonAsync(Url, new CreateTeacherNoteCommand(Guid.NewGuid(), "Nobody"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("notes.student_not_found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task An_empty_too_long_or_future_note_is_refused()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var student = scenario.Students[0].Id;

        var empty = await teacher.PostJsonAsync(Url, new CreateTeacherNoteCommand(student, "  "));
        var tooLong = await teacher.PostJsonAsync(Url, new CreateTeacherNoteCommand(student, new string('x', TeacherNote.BodyMaxLength + 1)));
        var future = await teacher.PostJsonAsync(Url, new CreateTeacherNoteCommand(student, "Tomorrow", TestClock.WibToday().AddDays(2)));
        var paging = await teacher.GetAsync($"{Url}?studentId={student}&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("notes.future_date", await future.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, paging.StatusCode);
    }

    [Fact]
    public async Task Students_parents_and_admin_staff_are_forbidden_by_policy()
    {
        var id = Guid.NewGuid();
        var clients = new[] { fixture.AsStudent(), fixture.AsParent(), fixture.AsAdmin(), fixture.AsPrincipal() };

        foreach (var client in clients)
        {
            using (client)
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}?studentId={id}")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, new CreateTeacherNoteCommand(id, "No"))).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Url}/{id}")).StatusCode);
            }
        }
    }
}
