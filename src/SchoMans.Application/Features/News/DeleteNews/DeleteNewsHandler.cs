using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.News.DeleteNews;

/// <summary>Soft delete: the article disappears everywhere, but its slug stays reserved so the old address never shows other content.</summary>
public sealed class DeleteNewsHandler(IAppDbContext db)
{
    public async Task<Result<Unit>> Handle(DeleteNewsCommand command, CancellationToken ct)
    {
        var article = await db.NewsArticles.FirstOrDefaultAsync(n => n.Id == command.Id, ct);
        if (article is null)
        {
            return Error.NotFound("news.not_found", "The article does not exist.");
        }

        db.NewsArticles.Remove(article);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
