using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.ListMyProgressSummaries;

/// <summary>
/// The history of sent summaries for the children of the signed-in parent. A child who is not theirs is refused; a draft or an
/// approved summary is never shown: the family only sees what the teacher sent.
/// </summary>
public sealed class ListMyProgressSummariesHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<ProgressSummaryDto>>> Handle(ListMyProgressSummariesQuery query, CancellationToken ct)
    {
        var childIds = await SchoolMembership.ChildIdsAsync(db, currentUser, ct);
        if (query.StudentId is { } studentId)
        {
            if (!childIds.Contains(studentId))
            {
                return Error.Forbidden("progress.not_your_child", "You can only read the summaries of your own children.");
            }

            childIds = [studentId];
        }

        var rows = db.ProgressSummaries.AsNoTracking()
            .Where(s => s.Status == ProgressSummaryStatus.Sent && childIds.Contains(s.StudentId))
            .OrderByDescending(s => s.Month).ThenBy(s => s.Student!.User!.FullName)
            .Select(s => new { Summary = s, Name = s.Student!.User!.FullName });

        var page = await rows.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<ProgressSummaryDto>(
            page.Items.Select(r => ProgressSummaryMapping.ToDto(r.Summary, r.Name)).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}
