using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Notifications.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<Unit>> Handle(MarkAllNotificationsReadCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var now = timeProvider.GetUtcNow();
        foreach (var notification in await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync(ct))
        {
            notification.MarkRead(now);
        }

        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
