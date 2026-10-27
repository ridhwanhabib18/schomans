using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.News.PublishNews;

public sealed class PublishNewsHandler(IAppDbContext db, IFileStorage storage, TimeProvider timeProvider)
{
    public async Task<Result<NewsDto>> Handle(PublishNewsCommand command, CancellationToken ct)
    {
        var article = await db.NewsArticles.FirstOrDefaultAsync(n => n.Id == command.Id, ct);
        if (article is null)
        {
            return Error.NotFound("news.not_found", "The article does not exist.");
        }

        if (!article.Publish(timeProvider.GetUtcNow()))
        {
            return Error.Conflict("news.already_published", "The article is already published.");
        }

        await db.SaveChangesAsync(ct);
        return NewsMapping.ToDto(article, storage);
    }
}
