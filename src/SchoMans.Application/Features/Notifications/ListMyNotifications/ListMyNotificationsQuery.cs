namespace SchoMans.Application.Features.Notifications.ListMyNotifications;

public sealed record ListMyNotificationsQuery(int Page = 1, int PageSize = 20, bool UnreadOnly = false);
