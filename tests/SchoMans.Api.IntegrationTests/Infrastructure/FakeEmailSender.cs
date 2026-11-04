using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>Stands in for the SMTP server: remembers what was sent, and can be told to be unconfigured or to fail.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly List<SentEmail> _sent = [];

    public bool IsConfigured { get; set; } = true;

    /// <summary>When set, every send throws it.</summary>
    public Exception? Failure { get; set; }

    public IReadOnlyList<SentEmail> Sent
    {
        get
        {
            lock (_sent)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(string toAddress, string subject, string body, CancellationToken ct)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        lock (_sent)
        {
            _sent.Add(new SentEmail(toAddress, subject, body));
        }

        return Task.CompletedTask;
    }
}

public sealed record SentEmail(string To, string Subject, string Body);
