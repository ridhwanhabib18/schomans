namespace SchoMans.Application.Common.Interfaces;

/// <summary>Sends one plain-text email through the mail server (SMTP; Mailpit catches the mail locally). Throws when the mail cannot be sent.</summary>
public interface IEmailSender
{
    /// <summary>False when no mail server is configured; the queue then stays as it is.</summary>
    bool IsConfigured { get; }

    Task SendAsync(string toAddress, string subject, string body, CancellationToken ct);
}
