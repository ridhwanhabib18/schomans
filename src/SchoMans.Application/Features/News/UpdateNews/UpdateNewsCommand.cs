using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News.UpdateNews;

/// <param name="CoverImageKey">The key from an upload (see the upload-url use case), or null for no cover image.</param>
public sealed record UpdateNewsCommand(
    Guid Id,
    string Title,
    string Body,
    NewsCategory Category,
    string? MetaDescription,
    string? CoverImageKey,
    string RowVersion);
