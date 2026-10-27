using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News.GetPublicNews;

public sealed class GetPublicNewsHandler(IAppDbContext db, IFileStorage storage, IHtmlContentSanitizer sanitizer)
{
    public async Task<Result<PublicNewsDto>> Handle(GetPublicNewsQuery query, CancellationToken ct)
    {
        // A draft is "not found" for the public, never "forbidden": nothing hints that the slug exists.
        var article = await db.NewsArticles.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Slug == query.Slug && n.Status == PublishStatus.Published, ct);
        if (article is null)
        {
            return Error.NotFound("news.not_found", "The article does not exist.");
        }

        return new PublicNewsDto(
            article.Slug,
            article.Title,
            article.Body,
            article.CoverImageKey is null ? null : storage.GetPublicUrl(article.CoverImageKey),
            article.Category.ToString(),
            NewsMapping.MetaDescriptionOf(article, sanitizer),
            article.PublishedAt!.Value);
    }
}
