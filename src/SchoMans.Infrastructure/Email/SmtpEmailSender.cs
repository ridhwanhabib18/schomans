using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Email;

/// <summary>Configuration section <c>Email</c>. With an empty <see cref="Host"/> no mail is sent and the queue just waits.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1025;

    public bool UseSsl { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "no-reply@schomans.local";

    public string FromName { get; set; } = "SchoMans";
}

/// <summary>Sends mail through an SMTP server with the framework's own client; Mailpit plays the server locally.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Host);

    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.To.Add(new MailAddress(toAddress));

        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.UseSsl, Timeout = 15_000 };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, ct);
    }
}
