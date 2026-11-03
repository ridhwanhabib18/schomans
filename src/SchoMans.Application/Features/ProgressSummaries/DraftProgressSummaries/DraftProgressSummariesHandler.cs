using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.ProgressSummaries.DraftProgressSummaries;

/// <summary>
/// Run at the start of each month: every active student without a summary for the previous month gets a draft (when there is
/// data to write from). Safe to run again: students with a summary are skipped, so the next run picks up the ones a provider
/// failure left out. It stops early when the month's AI budget is used up.
/// </summary>
public sealed class DraftProgressSummariesHandler(IAppDbContext db, ISchoolClock clock, ProgressSummaryDrafter drafter)
{
    /// <returns>How many drafts were written.</returns>
    public async Task<int> Handle(CancellationToken ct)
    {
        var month = ProgressSummary.MonthKey(clock.Today.AddMonths(-1));

        var studentIds = await db.Students.AsNoTracking()
            .Where(s => s.Status == StudentStatus.Active
                && !db.ProgressSummaries.Any(p => p.StudentId == s.Id && p.Month == month))
            .Select(s => s.Id)
            .ToListAsync(ct);

        var drafted = 0;
        foreach (var studentId in studentIds)
        {
            var result = await drafter.DraftAsync(null, studentId, month, ct);
            if (result.IsSuccess)
            {
                drafted++;
            }
            else if (result.Error.Code == "ai.budget_exhausted")
            {
                break;
            }
        }

        return drafted;
    }
}
