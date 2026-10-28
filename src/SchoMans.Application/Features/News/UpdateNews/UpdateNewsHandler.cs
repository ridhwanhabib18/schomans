using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Files;

namespace SchoMans.Application.Features.News.UpdateNews;

public sealed class UpdateNewsHandler(IAppDbContext db, IHtmlContentSanitizer sanitizer, IFileStorage storage)
{
    public async Task<Result<NewsDto>> Handle(UpdateNewsCommand command, CancellationToken ct)
    {
        var article = await db.NewsArticles.FirstOrDefaultAsync(n => n.Id == command.Id, ct);
        if (article is null)
        {
            return Error.NotFound("news.not_found", "The article does not exist.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        var coverKey = string.IsNullOrWhiteSpace(command.CoverImageKey) ? null : command.CoverImageKey.Trim();
        var coverChanged = coverKey != article.CoverImageKey;
        if (coverChanged && await CoverImages.VerifyAsync(storage, CoverImages.NewsModule, article.Id, coverKey, ct) is { } coverError)
        {
            return coverError;
        }

        var previousCover = article.CoverImageKey;
        article.Update(command.Title, sanitizer.Sanitize(command.Body), command.Category, command.MetaDescription);
        article.SetCover(coverKey);
        db.SetExpectedRowVersion(article, rowVersion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommonErrors.ConcurrencyConflict;
        }

        // The old image is no longer referenced; removing it is housekeeping, so a failure here must not fail the save.
        if (coverChanged && previousCover is not null)
        {
            try
            {
                await storage.DeleteAsync(FileBucket.Public, previousCover, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // An orphaned file costs a few kilobytes; a lifecycle rule can sweep it later.
            }
        }

        return NewsMapping.ToDto(article, storage);
    }
}
