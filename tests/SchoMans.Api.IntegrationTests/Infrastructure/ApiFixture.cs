using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Owns the SQL Server for the whole test run. By default it starts a SQL Server container with Testcontainers.
/// Where the Docker API is not reachable (for example Docker only inside WSL), set <c>SCHOMANS_TEST_SQL</c> to the
/// connection string of an already running SQL Server (without a database name); a throw-away database is created on it.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string ExternalSqlVariable = "SCHOMANS_TEST_SQL";

    private MsSqlContainer? _container;
    private string _serverConnectionString = string.Empty;
    private string? _databaseName;

    public ApiFactory Factory { get; private set; } = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalSqlVariable);
        if (string.IsNullOrWhiteSpace(external))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            _serverConnectionString = _container.GetConnectionString();
        }
        else
        {
            _serverConnectionString = external;
        }

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

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        else if (_databaseName is not null)
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(_serverConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
