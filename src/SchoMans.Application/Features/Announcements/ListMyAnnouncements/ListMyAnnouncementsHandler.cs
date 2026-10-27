using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements.ListMyAnnouncements;

public sealed class ListMyAnnouncementsHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, IHtmlContentSanitizer sanitizer)
{
    public async Task<Result<PagedResult<AnnouncementSummaryDto>>> Handle(ListMyAnnouncementsQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var classroomIds = await AnnouncementAudience.ClassroomIdsOfAsync(db, currentUser, ct);

        var announcements = db.Announcements.AsNoTracking()
            .Where(a => a.Status == PublishStatus.Published)
            .Where(AnnouncementAudience.VisibleTo(currentUser.Role, classroomIds));

        if (query.UnreadOnly)
        {
            announcements = announcements.Where(a => !db.AnnouncementReads.Any(r => r.AnnouncementId == a.Id && r.UserId == userId));
        }

        var page = await announcements.OrderByDescending(a => a.PublishedAt).ThenBy(a => a.Id).ToPagedResultAsync(query.Page, query.PageSize, ct);

        var ids = page.Items.Select(a => a.Id).ToList();
        var read = (await db.AnnouncementReads.AsNoTracking()
            .Where(r => r.UserId == userId && ids.Contains(r.AnnouncementId))
            .Select(r => r.AnnouncementId)
            .ToListAsync(ct)).ToHashSet();

        return new PagedResult<AnnouncementSummaryDto>(
            page.Items.Select(a => AnnouncementMapping.ToSummary(a, read.Contains(a.Id), storage, sanitizer)).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}
