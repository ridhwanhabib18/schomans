using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Storage;

/// <summary>One implementation for every S3-compatible store: SeaweedFS locally, Cloudflare R2 in the demo.</summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly StorageOptions _options;
    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _signer;
    private readonly TimeProvider _timeProvider;
    private readonly Protocol _signedProtocol;

    public S3FileStorage(IOptions<StorageOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _client = CreateClient(_options.Endpoint);
        // Presigning is a local calculation. The URL is signed for the host in it, so it must be the browser's host.
        var browserEndpoint = string.IsNullOrWhiteSpace(_options.BrowserEndpoint) ? _options.Endpoint : _options.BrowserEndpoint;
        _signer = CreateClient(browserEndpoint);
        // The SDK signs https URLs by default; local SeaweedFS speaks plain http, so follow the scheme of the endpoint.
        _signedProtocol = browserEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS;
    }

    public Task<PresignedUpload> CreateUploadUrlAsync(FileBucket bucket, string key, string contentType, TimeSpan lifetime, CancellationToken ct)
    {
        var expires = _timeProvider.GetUtcNow().Add(lifetime);
        var url = _signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = BucketName(bucket),
            Key = key,
            Verb = HttpVerb.PUT,
            Protocol = _signedProtocol,
            Expires = expires.UtcDateTime,
            ContentType = contentType, // bound into the signature: the upload must send exactly this Content-Type
        });
        return Task.FromResult(new PresignedUpload(url, expires));
    }

    public async Task UploadAsync(FileBucket bucket, string key, byte[] content, string contentType, CancellationToken ct)
    {
        using var stream = new MemoryStream(content);
        await _client.PutObjectAsync(
            new PutObjectRequest { BucketName = BucketName(bucket), Key = key, InputStream = stream, ContentType = contentType },
            ct);
    }

    public string CreateDownloadUrl(string key, TimeSpan lifetime) =>
        _signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.PrivateBucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _signedProtocol,
            Expires = _timeProvider.GetUtcNow().Add(lifetime).UtcDateTime,
        });

    public string GetPublicUrl(string key) => $"{_options.PublicBaseUrl.TrimEnd('/')}/{key}";

    public async Task<StoredObject?> GetObjectInfoAsync(FileBucket bucket, string key, CancellationToken ct)
    {
        try
        {
            var metadata = await _client.GetObjectMetadataAsync(BucketName(bucket), key, ct);
            return new StoredObject(metadata.ContentLength, metadata.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(FileBucket bucket, string key, CancellationToken ct) =>
        _client.DeleteObjectAsync(BucketName(bucket), key, ct);

    /// <summary>Creates a bucket if it does not exist yet. Used at startup in development and the demo.</summary>
    public async Task EnsureBucketAsync(string bucketName, CancellationToken ct)
    {
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }, ct);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // Already there.
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _signer.Dispose();
    }

    private string BucketName(FileBucket bucket) => bucket == FileBucket.Public ? _options.PublicBucket : _options.PrivateBucket;

    private AmazonS3Client CreateClient(string endpoint) => new(
        new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
        new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = _options.ForcePathStyle,
            AuthenticationRegion = _options.Region,
            // The newer SDK adds checksums to every request by default; S3-compatible stores such as SeaweedFS and R2
            // do not all accept them. "When required" keeps the classic behavior.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
}
