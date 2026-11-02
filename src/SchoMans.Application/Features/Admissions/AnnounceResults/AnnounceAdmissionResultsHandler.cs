using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Admissions;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Admissions.AnnounceResults;

/// <summary>
/// Runs every few minutes. When the announcement moment of a period has come, every applicant who has a decision is told
/// at once; the period is marked, so a second run does nothing. The decisions themselves became visible by the clock.
/// </summary>
public sealed class AnnounceAdmissionResultsHandler(IAppDbContext db, INotificationService notifications, ISchoolClock clock)
{
    public async Task<int> Handle(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var periods = await db.AdmissionPeriods.Where(p => p.ResultsAnnouncedAt <= now && p.ResultsNotifiedAt == null).ToListAsync(ct);

        var told = 0;
        foreach (var period in periods)
        {
            var decided = await db.Applicants.AsNoTracking()
                .Where(a => a.AdmissionPeriodId == period.Id && (a.Status == ApplicantStatus.Accepted || a.Status == ApplicantStatus.Rejected))
                .Select(a => new { a.UserId, a.Status })
                .ToListAsync(ct);

            var deadline = clock.ToSchoolDateTime(period.ReregistrationDeadline).ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
            told += await NotificationSender.SendAsync(
                db, notifications, decided.Where(a => a.Status == ApplicantStatus.Accepted).Select(a => a.UserId),
                NotificationType.AdmissionResult, "You are accepted", $"Confirm your place before {deadline}.", "/admissions", ct);
            told += await NotificationSender.SendAsync(
                db, notifications, decided.Where(a => a.Status == ApplicantStatus.Rejected).Select(a => a.UserId),
                NotificationType.AdmissionResult, "The result of your application is out", "Open your application to read it.", "/admissions", ct);

            period.MarkResultsNotified(now);
            await db.SaveChangesAsync(ct);
        }

        return told;
    }
}
