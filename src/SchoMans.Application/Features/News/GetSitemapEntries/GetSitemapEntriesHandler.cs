using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News.GetSitemapEntries;

/// <summary>The published articles for the public sitemap.xml that search engines read.</summary>
public sealed class GetSitemapEntriesHandler(IAppDbContext db)
{
    public async Task<Result<IReadOnlyList<SitemapEntry>>> Handle(GetSitemapEntriesQuery query, CancellationToken ct)
    {
        var entries = await db.NewsArticles.AsNoTracking()
            .Where(n => n.Status == PublishStatus.Published)
            .OrderByDescending(n => n.PublishedAt)
            .Select(n => new SitemapEntry(n.Slug, n.UpdatedAt))
            .Take(5000)
            .ToListAsync(ct);
        return entries;
    }
}
