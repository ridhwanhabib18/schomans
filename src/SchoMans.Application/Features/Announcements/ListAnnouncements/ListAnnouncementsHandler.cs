using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Announcements.ListAnnouncements;

public sealed class ListAnnouncementsHandler(IAppDbContext db, IFileStorage storage)
{
    public async Task<Result<PagedResult<AnnouncementDto>>> Handle(ListAnnouncementsQuery query, CancellationToken ct)
    {
        var announcements = db.Announcements.AsNoTracking();

        if (query.Status is { } status)
        {
            announcements = announcements.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            announcements = announcements.Where(a => a.Title.Contains(term));
        }

        var page = await announcements.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<AnnouncementDto>(
            page.Items.Select(a => AnnouncementMapping.ToDto(a, storage)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
