using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Files.CreateUploadUrl;

/// <summary>
/// Step 1 of an upload: after the role and ownership checks and the type and size validation, the browser gets a
/// short-lived presigned PUT URL and uploads straight to storage. The size is checked again when the key is saved.
/// </summary>
public sealed class CreateUploadUrlHandler(IAppDbContext db, IFileStorage storage)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<UploadUrlDto>> Handle(CreateUploadUrlCommand command, CancellationToken ct)
    {
        var exists = command.Module == CoverImages.NewsModule
            ? await db.NewsArticles.AnyAsync(n => n.Id == command.EntityId, ct)
            : await db.Announcements.AnyAsync(a => a.Id == command.EntityId, ct);
        if (!exists)
        {
            return Error.NotFound("files.entity_not_found", "Save the record first, then upload its image.");
        }

        var yearSegment = (await db.AcademicYears.AsNoTracking().Where(y => y.IsActive).Select(y => y.Name).FirstOrDefaultAsync(ct))
            ?.Replace('/', '-') ?? "none";
        var key = CoverImages.NewKey(command.Module, command.EntityId, command.ContentType, yearSegment);

        var upload = await storage.CreateUploadUrlAsync(CoverImages.BucketFor(command.Module), key, command.ContentType, Lifetime, ct);
        return new UploadUrlDto(upload.Url, key, command.ContentType, upload.ExpiresAt);
    }
}
