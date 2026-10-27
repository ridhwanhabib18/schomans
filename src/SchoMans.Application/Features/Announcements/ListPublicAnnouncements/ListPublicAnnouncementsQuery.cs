namespace SchoMans.Application.Features.Announcements.ListPublicAnnouncements;

/// <summary>Announcements marked public, for guests: published ones only, newest first.</summary>
public sealed record ListPublicAnnouncementsQuery(int Page = 1, int PageSize = 10);
