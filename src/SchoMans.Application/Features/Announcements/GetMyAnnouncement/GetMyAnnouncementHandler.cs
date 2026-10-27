using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements.GetMyAnnouncement;

public sealed class GetMyAnnouncementHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage)
{
    public async Task<Result<AnnouncementReaderDto>> Handle(GetMyAnnouncementQuery query, CancellationToken ct)
    {
        var classroomIds = await AnnouncementAudience.ClassroomIdsOfAsync(db, currentUser, ct);

        // An announcement meant for somebody else is "not found": nothing tells the reader that it exists.
        var announcement = await db.Announcements.AsNoTracking()
            .Where(a => a.Id == query.Id && a.Status == PublishStatus.Published)
            .Where(AnnouncementAudience.VisibleTo(currentUser.Role, classroomIds))
            .FirstOrDefaultAsync(ct);
        if (announcement is null)
        {
            return Error.NotFound("announcements.not_found", "The announcement does not exist.");
        }

        var userId = currentUser.UserId;
        var isRead = await db.AnnouncementReads.AnyAsync(r => r.AnnouncementId == announcement.Id && r.UserId == userId, ct);
        return AnnouncementMapping.ToReader(announcement, isRead, storage);
    }
}
