using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using SchoMans.Infrastructure.Storage;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A real S3-compatible store for the storage tests: SeaweedFS, started with Testcontainers using the same
/// <c>infra/seaweedfs/s3.json</c> as local development. Where the Docker API is not reachable (Docker only inside WSL),
/// set <c>SCHOMANS_TEST_S3</c> to the endpoint of a running SeaweedFS, for example <c>http://127.0.0.1:8333</c>.
/// </summary>
public sealed class SeaweedFixture : IAsyncLifetime
{
    public const string ExternalEndpointVariable = "SCHOMANS_TEST_S3";
    private const int S3Port = 8333;
    private const int MasterPort = 9333;

    private IContainer? _container;

    public StorageOptions Options { get; private set; } = new();

    public S3FileStorage Storage { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var endpoint = Environment.GetEnvironmentVariable(ExternalEndpointVariable);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "seaweedfs", "s3.json");
            _container = new ContainerBuilder("chrislusf/seaweedfs:4.50")
                .WithPortBinding(S3Port, assignRandomHostPort: true)
                .WithPortBinding(MasterPort, assignRandomHostPort: true) // the wait strategy below asks the master, so it must be published
                .WithCommand("server", "-dir=/data", "-s3", $"-s3.port={S3Port}", "-s3.config=/etc/seaweedfs/s3.json", "-master.volumeSizeLimitMB=64")
                .WithResourceMapping(await File.ReadAllBytesAsync(configPath), "/etc/seaweedfs/s3.json")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(MasterPort).ForPath("/cluster/status")))
                .Build();
            // Never hang a whole test run on a container that does not come up.
            using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await _container.StartAsync(startup.Token);
            endpoint = $"http://127.0.0.1:{_container.GetMappedPublicPort(S3Port)}";
        }

        Options = new StorageOptions
        {
            Endpoint = endpoint,
            BrowserEndpoint = endpoint,
            AccessKey = "schomans-dev-access-key",
            SecretKey = "schomans-dev-secret-key-change-me",
            PublicBaseUrl = $"{endpoint}/schomans-public",
            EnsureBuckets = true,
        };
        Storage = new S3FileStorage(Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System);

        // The S3 gateway needs a moment after the master is up.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await Storage.EnsureBucketAsync(Options.PrivateBucket, CancellationToken.None);
                await Storage.EnsureBucketAsync(Options.PublicBucket, CancellationToken.None);
                return;
            }
            catch (Exception) when (attempt < 30)
            {
                await Task.Delay(1000);
            }
        }
    }

    public S3FileStorage CreateStorage(Action<StorageOptions> change)
    {
        var copy = new StorageOptions
        {
            Endpoint = Options.Endpoint,
            BrowserEndpoint = Options.BrowserEndpoint,
            AccessKey = Options.AccessKey,
            SecretKey = Options.SecretKey,
            Region = Options.Region,
            ForcePathStyle = Options.ForcePathStyle,
            PrivateBucket = Options.PrivateBucket,
            PublicBucket = Options.PublicBucket,
            PublicBaseUrl = Options.PublicBaseUrl,
        };
        change(copy);
        return new S3FileStorage(Microsoft.Extensions.Options.Options.Create(copy), TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        Storage?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
