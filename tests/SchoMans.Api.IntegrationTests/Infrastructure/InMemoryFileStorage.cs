using System.Collections.Concurrent;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for S3 in the endpoint tests: a test "uploads" a file by calling <see cref="Put"/>, which is what the browser
/// does after it got a presigned URL. The real S3 code is tested against a real SeaweedFS in <c>S3FileStorageTests</c>.
/// </summary>
public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<(FileBucket Bucket, string Key), StoredObject> _objects = new();

    public int UploadUrlsCreated { get; private set; }

    public void Put(FileBucket bucket, string key, long sizeBytes = 1024, string contentType = "image/png") =>
        _objects[(bucket, key)] = new StoredObject(sizeBytes, contentType);

    public bool Contains(FileBucket bucket, string key) => _objects.ContainsKey((bucket, key));

    public Task<PresignedUpload> CreateUploadUrlAsync(FileBucket bucket, string key, string contentType, TimeSpan lifetime, CancellationToken ct)
    {
        UploadUrlsCreated++;
        return Task.FromResult(new PresignedUpload($"https://storage.test/{bucket}/{key}?signature=fake", DateTimeOffset.UtcNow.Add(lifetime)));
    }

    public Task UploadAsync(FileBucket bucket, string key, byte[] content, string contentType, CancellationToken ct)
    {
        _objects[(bucket, key)] = new StoredObject(content.Length, contentType);
        return Task.CompletedTask;
    }

    public string CreateDownloadUrl(string key, TimeSpan lifetime) => $"https://storage.test/private/{key}?signature=fake";

    public string GetPublicUrl(string key) => $"https://storage.test/public/{key}";

    public Task<StoredObject?> GetObjectInfoAsync(FileBucket bucket, string key, CancellationToken ct) =>
        Task.FromResult(_objects.TryGetValue((bucket, key), out var info) ? info : null);

    public Task DeleteAsync(FileBucket bucket, string key, CancellationToken ct)
    {
        _objects.TryRemove((bucket, key), out _);
        return Task.CompletedTask;
    }
}
