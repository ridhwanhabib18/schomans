using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Notifications.ListMyNotifications;

/// <summary>Only the signed-in user's own notifications: the second authorization layer is the user id in the query.</summary>
public sealed class ListMyNotificationsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<NotificationDto>>> Handle(ListMyNotificationsQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var notifications = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.UnreadOnly)
        {
            notifications = notifications.Where(n => n.ReadAt == null);
        }

        var page = await notifications.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<NotificationDto>(page.Items.Select(NotificationMapping.ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
