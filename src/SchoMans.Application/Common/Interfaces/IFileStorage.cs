namespace SchoMans.Application.Common.Interfaces;

public enum FileBucket
{
    /// <summary>Student and staff files. Never public: downloads use short-lived presigned URLs.</summary>
    Private,

    /// <summary>Public content such as news images, readable by anyone.</summary>
    Public,
}

public sealed record PresignedUpload(string Url, DateTimeOffset ExpiresAt);

public sealed record StoredObject(long SizeBytes, string? ContentType);

/// <summary>S3-compatible object storage (SeaweedFS locally, Cloudflare R2 in the demo). The database stores keys, never URLs.</summary>
public interface IFileStorage
{
    /// <summary>A presigned PUT URL: the browser uploads straight to storage so the API carries no file traffic.</summary>
    Task<PresignedUpload> CreateUploadUrlAsync(FileBucket bucket, string key, string contentType, TimeSpan lifetime, CancellationToken ct);

    /// <summary>Stores a file the server made itself (a report-card PDF), as opposed to one the browser uploads.</summary>
    Task UploadAsync(FileBucket bucket, string key, byte[] content, string contentType, CancellationToken ct);

    /// <summary>A presigned GET URL for an object of the private bucket.</summary>
    string CreateDownloadUrl(string key, TimeSpan lifetime);

    /// <summary>The public address of an object of the public bucket.</summary>
    string GetPublicUrl(string key);

    /// <summary>Looks the object up after an upload, so a key is only saved when the file really exists.</summary>
    Task<StoredObject?> GetObjectInfoAsync(FileBucket bucket, string key, CancellationToken ct);

    Task DeleteAsync(FileBucket bucket, string key, CancellationToken ct);
}
