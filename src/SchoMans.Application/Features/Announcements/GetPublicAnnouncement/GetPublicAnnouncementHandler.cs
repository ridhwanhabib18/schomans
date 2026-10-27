using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements.GetPublicAnnouncement;

public sealed class GetPublicAnnouncementHandler(IAppDbContext db, IFileStorage storage)
{
    public async Task<Result<AnnouncementReaderDto>> Handle(GetPublicAnnouncementQuery query, CancellationToken ct)
    {
        // An internal announcement is "not found" for a guest: nothing hints that it exists.
        var announcement = await db.Announcements.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == query.Id && a.Status == PublishStatus.Published && a.IsPublic, ct);
        return announcement is null
            ? Error.NotFound("announcements.not_found", "The announcement does not exist.")
            : AnnouncementMapping.ToReader(announcement, isRead: false, storage);
    }
}
