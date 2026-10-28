using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Announcements.DeleteAnnouncement;

public sealed class DeleteAnnouncementHandler(IAppDbContext db)
{
    public async Task<Result<Unit>> Handle(DeleteAnnouncementCommand command, CancellationToken ct)
    {
        var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == command.Id, ct);
        if (announcement is null)
        {
            return Error.NotFound("announcements.not_found", "The announcement does not exist.");
        }

        // The notifications of a deleted announcement would open a page that no longer exists.
        var link = $"/announcements/{announcement.Id}";
        db.Notifications.RemoveRange(await db.Notifications.Where(n => n.LinkUrl == link).ToListAsync(ct));
        db.Announcements.Remove(announcement);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
