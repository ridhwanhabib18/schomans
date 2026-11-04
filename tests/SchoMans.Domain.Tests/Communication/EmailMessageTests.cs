using SchoMans.Domain.Audit;
using SchoMans.Domain.Communication;

namespace SchoMans.Domain.Tests.Communication;

public class EmailMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static EmailMessage Create() => EmailMessage.Create(Guid.NewGuid(), " parent@school.test ", " Hello ", " Body ", "/requests/abc");

    [Fact]
    public void A_new_mail_is_pending_and_trimmed()
    {
        var mail = Create();

        Assert.Equal((EmailStatus.Pending, 0, "parent@school.test", "Hello", "Body"), (mail.Status, mail.Attempts, mail.ToAddress, mail.Subject, mail.Body));
        Assert.Null(mail.SentAt);
    }

    [Theory]
    [InlineData("", "Subject")]
    [InlineData("a@b.test", " ")]
    public void A_mail_needs_an_address_and_a_subject(string to, string subject) =>
        Assert.ThrowsAny<ArgumentException>(() => EmailMessage.Create(null, to, subject, "Body", "/x"));

    [Fact]
    public void Long_texts_are_shortened_to_the_column_sizes()
    {
        var mail = EmailMessage.Create(null, "a@b.test", new string('s', 500), new string('b', 5000), "/x");

        Assert.Equal(EmailMessage.SubjectMaxLength, mail.Subject.Length);
        Assert.Equal(EmailMessage.BodyMaxLength, mail.Body.Length);
    }

    [Fact]
    public void Sending_it_marks_it_sent_once_with_the_time()
    {
        var mail = Create();
        mail.MarkAttemptFailed("down");

        mail.MarkSent(Now);

        Assert.Equal((EmailStatus.Sent, 2, Now, null), (mail.Status, mail.Attempts, mail.SentAt, mail.LastError));
    }

    [Fact]
    public void A_failed_attempt_keeps_it_pending_until_the_last_one_gives_it_up()
    {
        var mail = Create();

        for (var attempt = 1; attempt < EmailMessage.MaxAttempts; attempt++)
        {
            mail.MarkAttemptFailed("down");
            Assert.Equal((EmailStatus.Pending, attempt), (mail.Status, mail.Attempts));
        }

        mail.MarkAttemptFailed(new string('e', 900));

        Assert.Equal((EmailStatus.Failed, EmailMessage.MaxAttempts), (mail.Status, mail.Attempts));
        Assert.Equal(EmailMessage.ErrorMaxLength, mail.LastError!.Length);
    }
}

public class AuditLogTests
{
    [Fact]
    public void An_entry_keeps_who_what_and_the_old_and_new_values()
    {
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();

        var log = AuditLog.Create(user, AuditAction.Updated, "Grade", id, """{"Score":70}""", """{"Score":85}""");

        Assert.Equal((user, AuditAction.Updated, "Grade", id), (log.UserId, log.Action, log.EntityName, log.EntityId));
        Assert.Equal(("""{"Score":70}""", """{"Score":85}"""), (log.OldValues, log.NewValues));
    }

    [Fact]
    public void A_background_job_leaves_no_user_and_an_entry_needs_an_entity_name()
    {
        Assert.Null(AuditLog.Create(null, AuditAction.Created, "Request", Guid.NewGuid(), null, "{}").UserId);
        Assert.ThrowsAny<ArgumentException>(() => AuditLog.Create(null, AuditAction.Created, " ", Guid.NewGuid(), null, "{}"));
    }
}
