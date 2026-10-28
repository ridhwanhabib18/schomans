using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SchoMans.Infrastructure.Storage;

public static class StorageInitializer
{
    /// <summary>Creates the private and public bucket when <c>Storage:EnsureBuckets</c> is on.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var options = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        if (!options.EnsureBuckets)
        {
            return;
        }

        var storage = services.GetRequiredService<S3FileStorage>();
        await storage.EnsureBucketAsync(options.PrivateBucket, ct);
        await storage.EnsureBucketAsync(options.PublicBucket, ct);
    }
}
