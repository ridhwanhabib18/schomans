using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.News.GetNews;

public sealed class GetNewsHandler(IAppDbContext db, IFileStorage storage)
{
    public async Task<Result<NewsDto>> Handle(GetNewsQuery query, CancellationToken ct)
    {
        var article = await db.NewsArticles.AsNoTracking().FirstOrDefaultAsync(n => n.Id == query.Id, ct);
        return article is null ? Error.NotFound("news.not_found", "The article does not exist.") : NewsMapping.ToDto(article, storage);
    }
}
