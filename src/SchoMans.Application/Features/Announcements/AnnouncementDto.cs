using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Files;
using SchoMans.Application.Features.News;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Announcements;

/// <summary>The announcement as its editors (admin and principal) see it, with its audience.</summary>
public sealed record AnnouncementDto(
    Guid Id,
    string Title,
    string Body,
    bool IsPublic,
    string? CoverImageKey,
    string? CoverImageUrl,
    IReadOnlyList<string> TargetRoles,
    IReadOnlyList<Guid> TargetClassroomIds,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    string RowVersion);

/// <summary>One line of a user's inbox.</summary>
public sealed record AnnouncementSummaryDto(Guid Id, string Title, string Excerpt, string? CoverImageUrl, DateTimeOffset PublishedAt, bool IsRead);

/// <summary>An announcement as a recipient or a guest reads it.</summary>
public sealed record AnnouncementReaderDto(Guid Id, string Title, string Body, string? CoverImageUrl, DateTimeOffset PublishedAt, bool IsRead);

internal static class AnnouncementMapping
{
    public static AnnouncementDto ToDto(Announcement a, IFileStorage storage) => new(
        a.Id,
        a.Title,
        a.Body,
        a.IsPublic,
        a.CoverImageKey,
        CoverImages.UrlFor(storage, CoverImages.AnnouncementsModule, a.CoverImageKey),
        a.TargetRoles.Select(r => r.ToString()).ToList(),
        a.TargetClassroomIds,
        a.Status.ToString(),
        a.PublishedAt,
        a.CreatedAt,
        RowVersionCodec.Encode(a.RowVersion));

    public static AnnouncementSummaryDto ToSummary(Announcement a, bool isRead, IFileStorage storage, IHtmlContentSanitizer sanitizer) => new(
        a.Id,
        a.Title,
        NewsMapping.Excerpt(sanitizer.ToPlainText(a.Body)),
        CoverImages.UrlFor(storage, CoverImages.AnnouncementsModule, a.CoverImageKey),
        a.PublishedAt!.Value,
        isRead);

    public static AnnouncementReaderDto ToReader(Announcement a, bool isRead, IFileStorage storage) => new(
        a.Id,
        a.Title,
        a.Body,
        CoverImages.UrlFor(storage, CoverImages.AnnouncementsModule, a.CoverImageKey),
        a.PublishedAt!.Value,
        isRead);
}
