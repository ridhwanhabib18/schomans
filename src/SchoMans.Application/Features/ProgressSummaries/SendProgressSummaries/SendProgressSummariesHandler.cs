using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.SendProgressSummaries;

/// <summary>
/// The bulk send after the review: every approved summary of the classroom and month becomes visible to the family and the
/// parents of the student are notified. Drafts that are not approved stay with the teacher. Sending twice sends nothing new.
/// </summary>
public sealed class SendProgressSummariesHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock, INotificationService notifications)
{
    public async Task<Result<SentProgressSummariesDto>> Handle(SendProgressSummariesCommand command, CancellationToken ct)
    {
        if (await ProgressSummaryAccess.RequireHomeroomAsync(db, currentUser, command.ClassroomId, ct) is { } denied)
        {
            return denied;
        }

        var approved = await db.ProgressSummaries
            .Where(s => s.Month == command.Month && s.Status == ProgressSummaryStatus.Approved && s.Student!.ClassroomId == command.ClassroomId)
            .ToListAsync(ct);

        var now = clock.UtcNow;
        foreach (var summary in approved)
        {
            summary.Send(now);
        }

        await db.SaveChangesAsync(ct);

        var parentsNotified = 0;
        foreach (var summary in approved)
        {
            var parentIds = await db.StudentParents.AsNoTracking()
                .Where(p => p.StudentId == summary.StudentId)
                .Select(p => p.ParentUserId)
                .ToListAsync(ct);
            var studentName = await db.Students.AsNoTracking().Where(s => s.Id == summary.StudentId).Select(s => s.User!.FullName).FirstAsync(ct);

            parentsNotified += await NotificationSender.SendAsync(
                db,
                notifications,
                parentIds,
                NotificationType.ProgressSummary,
                $"Progress summary of {studentName}",
                $"The summary of {MonthName(summary.Month)} is ready to read.",
                $"/progress-summaries?studentId={summary.StudentId}",
                ct);
        }

        return new SentProgressSummariesDto(approved.Count, parentsNotified);
    }

    private static string MonthName(string month) =>
        DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
}
