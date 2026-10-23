namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>All integration tests share one SQL Server and one host, so the migrations and the seed run once.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
