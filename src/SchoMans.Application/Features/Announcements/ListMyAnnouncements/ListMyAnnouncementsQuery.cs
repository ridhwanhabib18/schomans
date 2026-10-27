namespace SchoMans.Application.Features.Announcements.ListMyAnnouncements;

/// <summary>The announcements the signed-in user may read, newest first.</summary>
public sealed record ListMyAnnouncementsQuery(int Page = 1, int PageSize = 20, bool UnreadOnly = false);
