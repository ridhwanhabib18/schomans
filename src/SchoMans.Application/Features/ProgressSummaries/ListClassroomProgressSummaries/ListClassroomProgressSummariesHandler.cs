using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.ListClassroomProgressSummaries;

/// <summary>The summaries of one month for the students of a classroom, for its homeroom teacher to review.</summary>
public sealed class ListClassroomProgressSummariesHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<List<ProgressSummaryDto>>> Handle(ListClassroomProgressSummariesQuery query, CancellationToken ct)
    {
        if (await ProgressSummaryAccess.RequireHomeroomAsync(db, currentUser, query.ClassroomId, ct) is { } denied)
        {
            return denied;
        }

        var month = query.Month ?? ProgressSummary.MonthKey(clock.Today.AddMonths(-1));
        var rows = await db.ProgressSummaries.AsNoTracking()
            .Where(s => s.Month == month && s.Student!.ClassroomId == query.ClassroomId)
            .OrderBy(s => s.Student!.User!.FullName)
            .Select(s => new { Summary = s, Name = s.Student!.User!.FullName })
            .ToListAsync(ct);

        return rows.Select(r => ProgressSummaryMapping.ToDto(r.Summary, r.Name)).ToList();
    }
}
