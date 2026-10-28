using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News.ListPublicNews;

public sealed class ListPublicNewsHandler(IAppDbContext db, IFileStorage storage, IHtmlContentSanitizer sanitizer)
{
    public async Task<Result<PagedResult<PublicNewsSummaryDto>>> Handle(ListPublicNewsQuery query, CancellationToken ct)
    {
        var articles = db.NewsArticles.AsNoTracking().Where(n => n.Status == PublishStatus.Published);
        if (query.Category is { } category)
        {
            articles = articles.Where(n => n.Category == category);
        }

        var page = await articles.OrderByDescending(n => n.PublishedAt).ThenBy(n => n.Id).ToPagedResultAsync(query.Page, query.PageSize, ct);
        var items = page.Items
            .Select(n => new PublicNewsSummaryDto(
                n.Slug,
                n.Title,
                NewsMapping.MetaDescriptionOf(n, sanitizer),
                CoverUrl(n),
                n.Category.ToString(),
                n.PublishedAt!.Value))
            .ToList();
        return new PagedResult<PublicNewsSummaryDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private string? CoverUrl(NewsArticle n) => n.CoverImageKey is null ? null : storage.GetPublicUrl(n.CoverImageKey);
}
