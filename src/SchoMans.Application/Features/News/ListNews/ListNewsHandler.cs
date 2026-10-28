using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.News.ListNews;

public sealed class ListNewsHandler(IAppDbContext db, IFileStorage storage)
{
    public async Task<Result<PagedResult<NewsDto>>> Handle(ListNewsQuery query, CancellationToken ct)
    {
        var articles = db.NewsArticles.AsNoTracking();

        if (query.Status is { } status)
        {
            articles = articles.Where(n => n.Status == status);
        }

        if (query.Category is { } category)
        {
            articles = articles.Where(n => n.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            articles = articles.Where(n => n.Title.Contains(term));
        }

        // Newest activity first: drafts have no publication date, so order by the creation date.
        var page = await articles.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<NewsDto>(page.Items.Select(n => NewsMapping.ToDto(n, storage)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
