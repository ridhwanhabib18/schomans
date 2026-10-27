using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News.CreateNews;

public sealed class CreateNewsHandler(IAppDbContext db, IHtmlContentSanitizer sanitizer, IFileStorage storage)
{
    public async Task<Result<NewsDto>> Handle(CreateNewsCommand command, CancellationToken ct)
    {
        var slug = await UniqueSlugAsync(command.Title, ct);
        var article = NewsArticle.Create(command.Title, slug, sanitizer.Sanitize(command.Body), command.Category, command.MetaDescription);
        db.NewsArticles.Add(article);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("news.slug_taken", "An article with the same address was just created. Try again.");
        }

        return NewsMapping.ToDto(article, storage);
    }

    /// <summary>
    /// A slug is never reused, not even after a soft delete, so an old shared link can never show a different article.
    /// </summary>
    private async Task<string> UniqueSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = SlugGenerator.Slugify(title);
        var taken = (await db.NewsArticles.IgnoreQueryFilters()
            .Where(n => n.Slug.StartsWith(baseSlug))
            .Select(n => n.Slug)
            .ToListAsync(ct)).ToHashSet();

        for (var number = 1; ; number++)
        {
            var candidate = SlugGenerator.WithSuffix(baseSlug, number);
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
