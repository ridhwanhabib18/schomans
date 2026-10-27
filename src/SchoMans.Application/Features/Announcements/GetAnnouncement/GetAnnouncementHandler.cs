using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Announcements.GetAnnouncement;

public sealed class GetAnnouncementHandler(IAppDbContext db, IFileStorage storage)
{
    public async Task<Result<AnnouncementDto>> Handle(GetAnnouncementQuery query, CancellationToken ct)
    {
        var announcement = await db.Announcements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == query.Id, ct);
        return announcement is null
            ? Error.NotFound("announcements.not_found", "The announcement does not exist.")
            : AnnouncementMapping.ToDto(announcement, storage);
    }
}
