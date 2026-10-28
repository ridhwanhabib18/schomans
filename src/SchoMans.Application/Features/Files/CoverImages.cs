using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Files;

public static class CoverImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> ExtensionByContentType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };
}

/// <summary>Where cover images live and how a saved key is checked. The first path segment is the module, the one before the file name is the entity id.</summary>
internal static class CoverImages
{
    public const string NewsModule = "news";
    public const string AnnouncementsModule = "announcements";

    public static FileBucket BucketFor(string module) => module == NewsModule ? FileBucket.Public : FileBucket.Private;

    public static string NewKey(string module, Guid entityId, string contentType, string yearSegment)
    {
        var extension = CoverImageRules.ExtensionByContentType[contentType];
        var file = $"{Guid.NewGuid():N}.{extension}";
        return module == NewsModule
            ? $"{NewsModule}/{entityId}/{file}"
            : $"{AnnouncementsModule}/{yearSegment}/{entityId}/{file}";
    }

    /// <summary>The key must belong to this very entity, the file must exist, and it must be a small image. Returns an error or null.</summary>
    public static async Task<Error?> VerifyAsync(IFileStorage storage, string module, Guid entityId, string? key, CancellationToken ct)
    {
        if (key is null)
        {
            return null;
        }

        var parts = key.Split('/');
        if (parts.Length < 3 || parts[0] != module || parts[^2] != entityId.ToString())
        {
            return Error.Validation("files.invalid_key", "The cover image does not belong to this record.");
        }

        var info = await storage.GetObjectInfoAsync(BucketFor(module), key, ct);
        if (info is null)
        {
            return Error.Validation("files.not_uploaded", "The cover image was not uploaded. Upload it first.");
        }

        if (info.SizeBytes > CoverImageRules.MaxBytes || info.ContentType is null || !CoverImageRules.ExtensionByContentType.ContainsKey(info.ContentType))
        {
            await storage.DeleteAsync(BucketFor(module), key, ct);
            return Error.Validation("files.invalid_image", "The cover image must be a JPEG, PNG or WebP file of at most 5 MB.");
        }

        return null;
    }

    public static string? UrlFor(IFileStorage storage, string module, string? key) =>
        key is null ? null : module == NewsModule ? storage.GetPublicUrl(key) : storage.CreateDownloadUrl(key, TimeSpan.FromMinutes(5));
}
