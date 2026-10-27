using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.News.UnpublishNews;

public sealed class UnpublishNewsHandler(IAppDbContext db, IFileStorage storage)
{
    public async Task<Result<NewsDto>> Handle(UnpublishNewsCommand command, CancellationToken ct)
    {
        var article = await db.NewsArticles.FirstOrDefaultAsync(n => n.Id == command.Id, ct);
        if (article is null)
        {
            return Error.NotFound("news.not_found", "The article does not exist.");
        }

        if (!article.Unpublish())
        {
            return Error.Conflict("news.not_published", "The article is not published.");
        }

        await db.SaveChangesAsync(ct);
        return NewsMapping.ToDto(article, storage);
    }
}
