using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<Unit>> Handle(MarkNotificationReadCommand command, CancellationToken ct)
    {
        // Somebody else's notification is "not found", the same answer as for one that does not exist.
        var userId = currentUser.UserId;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == command.Id && n.UserId == userId, ct);
        if (notification is null)
        {
            return Error.NotFound("notifications.not_found", "The notification does not exist.");
        }

        if (notification.MarkRead(timeProvider.GetUtcNow()))
        {
            await db.SaveChangesAsync(ct);
        }

        return new Unit();
    }
}
