using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Owns a SQL Server database and a host for tests. As a collection fixture it is shared by all tests of the "Api"
/// collection; as a class fixture a test class gets a database of its own (needed by tests that change global state,
/// such as the academic-year rollover). By default it starts a SQL Server container with Testcontainers.
/// Where the Docker API is not reachable (for example Docker only inside WSL), set <c>SCHOMANS_TEST_SQL</c> to the
/// connection string of an already running SQL Server (without a database name); a throw-away database is created on it.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string ExternalSqlVariable = "SCHOMANS_TEST_SQL";

    private static readonly SemaphoreSlim ContainerLock = new(1, 1);
    private static MsSqlContainer? _sharedContainer;
    private static int _fixtureCount;

    private string _serverConnectionString = string.Empty;
    private string? _databaseName;

    public ApiFactory Factory { get; private set; } = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public InMemoryFileStorage Storage => Factory.Storage;

    public async Task InitializeAsync()
    {
        _serverConnectionString = await AcquireServerAsync();

        _databaseName = $"SchoMans_Test_{Guid.NewGuid():N}";
        await using (var connection = new SqlConnection(_serverConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        ConnectionString = new SqlConnectionStringBuilder(_serverConnectionString) { InitialCatalog = _databaseName }.ConnectionString;
        Factory = new ApiFactory(ConnectionString);

        // Starts the host once: applies the migrations and seeds the demo data.
        using var warmUp = Factory.CreateClient();
    }

    /// <summary>A second host on the same database with extra settings (for example a tiny rate limit).</summary>
    public ApiFactory CreateFactory(IReadOnlyDictionary<string, string> settings, string environment = "Testing") =>
        new(ConnectionString, environment, settings);

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_databaseName is not null)
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(_serverConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        await ReleaseServerAsync();
    }

    // One SQL Server container serves every fixture of the run; each fixture only creates its own database on it.
    private static async Task<string> AcquireServerAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalSqlVariable);
        if (!string.IsNullOrWhiteSpace(external))
        {
            return external;
        }

        await ContainerLock.WaitAsync();
        try
        {
            if (_sharedContainer is null)
            {
                _sharedContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await _sharedContainer.StartAsync();
            }

            _fixtureCount++;
            return _sharedContainer.GetConnectionString();
        }
        finally
        {
            ContainerLock.Release();
        }
    }

    private static async Task ReleaseServerAsync()
    {
        await ContainerLock.WaitAsync();
        try
        {
            if (_sharedContainer is not null && --_fixtureCount == 0)
            {
                await _sharedContainer.DisposeAsync();
                _sharedContainer = null;
            }
        }
        finally
        {
            ContainerLock.Release();
        }
    }
}
