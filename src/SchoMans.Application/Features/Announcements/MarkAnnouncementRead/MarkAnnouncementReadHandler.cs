using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements.MarkAnnouncementRead;

/// <summary>Idempotent: opening an announcement twice records one read. The matching notification is marked read too.</summary>
public sealed class MarkAnnouncementReadHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<Unit>> Handle(MarkAnnouncementReadCommand command, CancellationToken ct)
    {
        var classroomIds = await AnnouncementAudience.ClassroomIdsOfAsync(db, currentUser, ct);
        var visible = await db.Announcements.AsNoTracking()
            .Where(a => a.Id == command.Id && a.Status == PublishStatus.Published)
            .Where(AnnouncementAudience.VisibleTo(currentUser.Role, classroomIds))
            .AnyAsync(ct);
        if (!visible)
        {
            return Error.NotFound("announcements.not_found", "The announcement does not exist.");
        }

        var userId = currentUser.UserId;
        var now = timeProvider.GetUtcNow();

        if (!await db.AnnouncementReads.AnyAsync(r => r.AnnouncementId == command.Id && r.UserId == userId, ct))
        {
            db.AnnouncementReads.Add(AnnouncementRead.Create(command.Id, userId, now));
        }

        var link = $"/announcements/{command.Id}";
        foreach (var notification in await db.Notifications.Where(n => n.UserId == userId && n.LinkUrl == link && n.ReadAt == null).ToListAsync(ct))
        {
            notification.MarkRead(now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A parallel request recorded the same read first; the result is the same.
            db.ChangeTracker.Clear();
        }

        return new Unit();
    }
}
