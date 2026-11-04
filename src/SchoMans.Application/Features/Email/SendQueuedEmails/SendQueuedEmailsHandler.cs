using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Email.SendQueuedEmails;

public sealed record SentEmailsDto(int Sent, int Failed);

/// <summary>
/// Runs every minute: sends the queued emails, oldest first. A mail that cannot be sent is tried again on the next runs and
/// given up after <see cref="EmailMessage.MaxAttempts"/>; one bad address never holds back the others.
/// </summary>
public sealed class SendQueuedEmailsHandler(IAppDbContext db, IEmailSender sender, ISiteLinks links, ISchoolClock clock)
{
    private const int BatchSize = 50;

    public async Task<SentEmailsDto> Handle(CancellationToken ct)
    {
        if (!sender.IsConfigured)
        {
            return new SentEmailsDto(0, 0);
        }

        var pending = await db.EmailMessages
            .Where(m => m.Status == EmailStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        int sent = 0, failed = 0;
        foreach (var message in pending)
        {
            try
            {
                await sender.SendAsync(message.ToAddress, message.Subject, WithLink(message), ct);
                message.MarkSent(clock.UtcNow);
                sent++;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                message.MarkAttemptFailed(e.Message);
                failed++;
            }

            // Saved one by one: a crash half-way must not send a mail twice on the next run.
            await db.SaveChangesAsync(ct);
        }

        return new SentEmailsDto(sent, failed);
    }

    private string WithLink(EmailMessage message) => $"{message.Body}{Environment.NewLine}{Environment.NewLine}{links.PublicUrl(message.LinkUrl)}";
}
