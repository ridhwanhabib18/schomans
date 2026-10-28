namespace SchoMans.Infrastructure.Storage;

/// <summary>
/// Settings of the S3-compatible storage. Locally this is SeaweedFS in Docker; in the demo it is Cloudflare R2.
/// The two differ only in this configuration, never in code.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Where the API itself talks to storage (inside Docker this can be a service name).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Where the browser reaches storage. Presigned URLs are signed for this host, so it must be what the browser can open.
    /// Defaults to <see cref="Endpoint"/>.
    /// </summary>
    public string? BrowserEndpoint { get; set; }

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    /// <summary>SeaweedFS needs path-style addresses (host/bucket/key); R2 and AWS also accept them.</summary>
    public bool ForcePathStyle { get; set; } = true;

    public string PrivateBucket { get; set; } = "schomans-private";

    public string PublicBucket { get; set; } = "schomans-public";

    /// <summary>The address in front of the public bucket, for example <c>http://localhost:8333/schomans-public</c> or a CDN domain.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>Creates the two buckets at startup when they are missing (local development and the demo).</summary>
    public bool EnsureBuckets { get; set; }
}
