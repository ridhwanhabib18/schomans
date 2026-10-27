using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Files;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News;

/// <summary>The full article as the editor sees it (admin and principal).</summary>
public sealed record NewsDto(
    Guid Id,
    string Title,
    string Slug,
    string Body,
    string? CoverImageKey,
    string? CoverImageUrl,
    string Category,
    string? MetaDescription,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    string RowVersion);

/// <summary>One line of the public news list.</summary>
public sealed record PublicNewsSummaryDto(string Slug, string Title, string Excerpt, string? CoverImageUrl, string Category, DateTimeOffset PublishedAt);

/// <summary>An article on the public page; <see cref="MetaDescription"/> feeds the page's meta tags.</summary>
public sealed record PublicNewsDto(
    string Slug,
    string Title,
    string Body,
    string? CoverImageUrl,
    string Category,
    string MetaDescription,
    DateTimeOffset PublishedAt);

internal static class NewsMapping
{
    public const int ExcerptLength = 160;

    public static NewsDto ToDto(NewsArticle n, IFileStorage storage) => new(
        n.Id,
        n.Title,
        n.Slug,
        n.Body,
        n.CoverImageKey,
        CoverImages.UrlFor(storage, CoverImages.NewsModule, n.CoverImageKey),
        n.Category.ToString(),
        n.MetaDescription,
        n.Status.ToString(),
        n.PublishedAt,
        n.CreatedAt,
        RowVersionCodec.Encode(n.RowVersion));

    /// <summary>The meta description of the article, or the start of its text when the editor did not write one.</summary>
    public static string MetaDescriptionOf(NewsArticle n, IHtmlContentSanitizer sanitizer) =>
        n.MetaDescription ?? Excerpt(sanitizer.ToPlainText(n.Body));

    public static string Excerpt(string plainText)
    {
        var text = plainText.Trim();
        return text.Length <= ExcerptLength ? text : text[..(ExcerptLength - 1)].TrimEnd() + "…";
    }
}
