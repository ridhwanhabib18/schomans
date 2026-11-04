using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Announcements;
using SchoMans.Application.Features.Announcements.CreateAnnouncement;
using SchoMans.Application.Features.Announcements.PublishAnnouncement;
using SchoMans.Application.Features.Email.SendQueuedEmails;
using SchoMans.Application.Features.Requests;
using SchoMans.Application.Features.Requests.CreateRequest;
using SchoMans.Application.Features.Requests.DecideRequest;
using SchoMans.Application.Features.Students.ListMyChildren;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-11 email notifications: the decision on a request, the announcement of an admission result and a published announcement queue
/// an email next to the in-app notification, and a job sends the queue through the mail server (here a fake).
/// </summary>
[Collection(ApiCollection.Name)]
public class EmailQueueTests(ApiFixture fixture)
{
    private async Task<List<EmailMessage>> QueuedForAsync(string address)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailMessages.AsNoTracking()
            .Where(m => m.ToAddress == address).OrderBy(m => m.CreatedAt).ToListAsync();
    }

    private async Task<EmailMessage> ParentMailAsync(Guid requestId) =>
        Assert.Single(await QueuedForAsync("parent@schomans.demo"), m => m.LinkUrl == $"/requests/{requestId}");

    /// <summary>Marks everything already queued as sent, so that a run of the sender only meets the mails of the test.</summary>
    private async Task DrainQueueAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailMessages
            .Where(m => m.Status == EmailStatus.Pending)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, EmailStatus.Sent));
    }

    private async Task<SentEmailsDto> RunSenderAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SendQueuedEmailsHandler>().Handle(CancellationToken.None);
    }

    private async Task<RequestDetailDto> DecidedLeaveAsync(bool approve)
    {
        using var parent = fixture.AsParent();
        using var teacher = fixture.AsTeacher();
        using var principal = fixture.AsPrincipal();
        var children = await (await parent.GetAsync("/api/v1/users/me/children")).ReadAsync<List<ChildDto>>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var created = await (await parent.PostJsonAsync("/api/v1/requests", new CreateRequestCommand(
            RequestType.StudentLeave, children.Single(c => c.FullName == "Rina Putri").StudentId, today.AddDays(1), today.AddDays(2), "Fever"))).ReadAsync<RequestDetailDto>();

        (await teacher.PostJsonAsync($"/api/v1/requests/{created.Id}/decision", new DecideRequestCommand(created.Id, true, null))).EnsureSuccessStatusCode();
        (await principal.PostJsonAsync($"/api/v1/requests/{created.Id}/decision", new DecideRequestCommand(created.Id, approve, approve ? null : "Exam week"))).EnsureSuccessStatusCode();
        return created;
    }

    [Fact]
    public async Task The_requester_gets_an_email_when_the_request_is_approved_but_not_for_an_intermediate_step()
    {
        await DrainQueueAsync();
        var created = await DecidedLeaveAsync(approve: true);

        var mail = await ParentMailAsync(created.Id);
        Assert.Equal(("Your request was approved", EmailStatus.Pending, 0), (mail.Subject, mail.Status, mail.Attempts));
    }

    [Fact]
    public async Task A_rejection_email_carries_the_reason()
    {
        await DrainQueueAsync();
        var created = await DecidedLeaveAsync(approve: false);

        var mail = await ParentMailAsync(created.Id);

        Assert.Equal("Your request was rejected", mail.Subject);
        Assert.Contains("Exam week", mail.Body);
    }

    [Fact]
    public async Task Publishing_an_announcement_queues_one_email_per_recipient()
    {
        using var admin = fixture.AsAdmin();
        var title = $"Sports day {Guid.NewGuid():N}";
        var draft = await (await admin.PostJsonAsync("/api/v1/announcements", new CreateAnnouncementCommand(title, "<p>Bring a water bottle.</p>", false, [UserRole.Parent], []))).ReadAsync<AnnouncementDto>();

        var summary = await (await admin.PostAsync($"/api/v1/announcements/{draft.Id}/publish", null)).ReadAsync<PublishAnnouncementSummary>();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var queued = await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailMessages.AsNoTracking()
            .Where(m => m.LinkUrl == $"/announcements/{draft.Id}").ToListAsync();
        Assert.Equal(summary.NotifiedUsers, queued.Count);
        Assert.All(queued, m => Assert.Equal((title, "Bring a water bottle."), (m.Subject, m.Body)));
        Assert.Contains(queued, m => m.ToAddress == "parent@schomans.demo");
    }

    [Fact]
    public async Task The_sender_job_sends_the_pending_mail_with_a_full_link_and_marks_it_sent()
    {
        await DrainQueueAsync();
        var created = await DecidedLeaveAsync(approve: true);
        fixture.Email.Failure = null;

        var run = await RunSenderAsync();

        Assert.True(run.Sent >= 1);
        var sent = Assert.Single(fixture.Email.Sent, e => e.Body.Contains($"https://school.test/requests/{created.Id}"));
        Assert.Equal(("parent@schomans.demo", "Your request was approved"), (sent.To, sent.Subject));
        var mail = await ParentMailAsync(created.Id);
        Assert.Equal((EmailStatus.Sent, 1), (mail.Status, mail.Attempts));
        Assert.NotNull(mail.SentAt);

        var again = await RunSenderAsync();
        Assert.Equal(0, again.Sent);
    }

    [Fact]
    public async Task A_mail_that_cannot_be_sent_is_tried_again_and_given_up_after_five_attempts()
    {
        await DrainQueueAsync();
        var created = await DecidedLeaveAsync(approve: true);
        fixture.Email.Failure = new InvalidOperationException("The mail server is down");
        try
        {
            for (var attempt = 1; attempt < EmailMessage.MaxAttempts; attempt++)
            {
                var run = await RunSenderAsync();
                Assert.Equal(0, run.Sent);
                var mail = await ParentMailAsync(created.Id);
                Assert.Equal((EmailStatus.Pending, attempt, "The mail server is down"), (mail.Status, mail.Attempts, mail.LastError));
            }

            await RunSenderAsync();
            var given = await ParentMailAsync(created.Id);
            Assert.Equal((EmailStatus.Failed, EmailMessage.MaxAttempts), (given.Status, given.Attempts));

            fixture.Email.Failure = null;
            await RunSenderAsync();
            Assert.DoesNotContain(fixture.Email.Sent, e => e.Body.Contains($"/requests/{created.Id}"));
        }
        finally
        {
            fixture.Email.Failure = null;
        }
    }

    [Fact]
    public async Task Without_a_mail_server_the_queue_just_waits()
    {
        await DrainQueueAsync();
        var created = await DecidedLeaveAsync(approve: true);
        fixture.Email.IsConfigured = false;
        try
        {
            var run = await RunSenderAsync();

            Assert.Equal(new SentEmailsDto(0, 0), run);
            var mail = await ParentMailAsync(created.Id);
            Assert.Equal((EmailStatus.Pending, 0), (mail.Status, mail.Attempts));
        }
        finally
        {
            fixture.Email.IsConfigured = true;
        }
    }

    [Fact]
    public async Task One_run_sends_every_pending_mail()
    {
        await DrainQueueAsync();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.EmailMessages.Add(EmailMessage.Create(null, "first@test.example", "First", "One", "/dashboard"));
            db.EmailMessages.Add(EmailMessage.Create(null, "second@test.example", "Second", "Two", "/dashboard"));
            await db.SaveChangesAsync();
        }

        var run = await RunSenderAsync();

        Assert.Equal(new SentEmailsDto(2, 0), run);
        Assert.Equal(["first@test.example", "second@test.example"], fixture.Email.Sent.Where(e => e.To.EndsWith("@test.example")).Select(e => e.To).Order().TakeLast(2));
    }
}
