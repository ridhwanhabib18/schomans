using SchoMans.Domain.Common;

namespace SchoMans.Domain.Communication;

public enum EmailStatus
{
    Pending,
    Sent,
    Failed,
}

/// <summary>
/// An email waiting to be sent (F-11). Handlers only queue it, in the same save as the in-app notification; a recurring job
/// sends the queue, so a mail server that is down never fails a request or loses a mail.
/// </summary>
public sealed class EmailMessage : BaseEntity
{
    public const int MaxAttempts = 5;
    public const int SubjectMaxLength = 200;
    public const int BodyMaxLength = 2000;
    public const int ErrorMaxLength = 500;
    public const int LinkMaxLength = 300;

    private EmailMessage()
    {
    }

    public Guid? UserId { get; private set; }

    public string ToAddress { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>The app path the mail points to; the sender job turns it into a full address.</summary>
    public string LinkUrl { get; private set; } = string.Empty;

    public EmailStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public static EmailMessage Create(Guid? userId, string toAddress, string subject, string body, string linkUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return new EmailMessage { UserId = userId, ToAddress = toAddress.Trim(), Subject = Shorten(subject.Trim(), SubjectMaxLength), Body = Shorten(body.Trim(), BodyMaxLength), LinkUrl = linkUrl };
    }

    public void MarkSent(DateTimeOffset now)
    {
        Attempts++;
        Status = EmailStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    /// <summary>Records a failed attempt; after <see cref="MaxAttempts"/> the mail is given up (Failed), before that it stays Pending for the next run.</summary>
    public void MarkAttemptFailed(string error)
    {
        Attempts++;
        LastError = Shorten(error, ErrorMaxLength);
        Status = Attempts >= MaxAttempts ? EmailStatus.Failed : EmailStatus.Pending;
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
