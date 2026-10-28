using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements.ListPublicAnnouncements;

public sealed class ListPublicAnnouncementsHandler(IAppDbContext db, IFileStorage storage, IHtmlContentSanitizer sanitizer)
{
    public async Task<Result<PagedResult<AnnouncementSummaryDto>>> Handle(ListPublicAnnouncementsQuery query, CancellationToken ct)
    {
        var page = await db.Announcements.AsNoTracking()
            .Where(a => a.Status == PublishStatus.Published && a.IsPublic)
            .OrderByDescending(a => a.PublishedAt)
            .ThenBy(a => a.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<AnnouncementSummaryDto>(
            page.Items.Select(a => AnnouncementMapping.ToSummary(a, isRead: false, storage, sanitizer)).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}
