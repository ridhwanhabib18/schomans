using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Notifications.GetUnreadNotificationCount;

public sealed class GetUnreadNotificationCountHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<UnreadNotificationCountDto>> Handle(GetUnreadNotificationCountQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var count = await db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
        return new UnreadNotificationCountDto(count);
    }
}
