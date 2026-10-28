using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements.ListAnnouncements;

/// <summary>The editors' list: drafts and published announcements.</summary>
public sealed record ListAnnouncementsQuery(int Page = 1, int PageSize = 20, string? Search = null, PublishStatus? Status = null);
