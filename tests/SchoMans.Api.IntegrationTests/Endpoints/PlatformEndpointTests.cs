using System.Net;
using System.Text.Json;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Api.Middleware;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>Health, correlation ids, ProblemDetails for unknown routes, CORS and the API docs.</summary>
[Collection(ApiCollection.Name)]
public class PlatformEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Live_health_check_is_anonymous_and_healthy()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_health_check_is_unhealthy_when_keycloak_cannot_be_reached()
    {
        // SQL Server is up, but the test host points Keycloak at a host that does not exist.
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Every_response_carries_a_correlation_id_and_an_incoming_one_is_kept()
    {
        using var client = fixture.Factory.CreateClient();

        var generated = await client.GetAsync("/health/live");
        Assert.True(generated.Headers.Contains(CorrelationIdMiddleware.HeaderName));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "my-trace-123");
        var echoed = await client.SendAsync(request);
        Assert.Equal("my-trace-123", echoed.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Cors_allows_the_web_origin_and_rejects_others()
    {
        using var client = fixture.Factory.CreateClient();

        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/v1/dashboard");
        allowed.Headers.Add("Origin", "http://localhost:5173");
        allowed.Headers.Add("Access-Control-Request-Method", "GET");
        allowed.Headers.Add("Access-Control-Request-Headers", "authorization");
        var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal("http://localhost:5173", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var rejected = new HttpRequestMessage(HttpMethod.Options, "/api/v1/dashboard");
        rejected.Headers.Add("Origin", "http://evil.example");
        rejected.Headers.Add("Access-Control-Request-Method", "GET");
        var rejectedResponse = await client.SendAsync(rejected);
        Assert.False(rejectedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Api_docs_are_not_public_outside_development_and_demo()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Demo_environment_publishes_named_operations_and_the_keycloak_scheme()
    {
        await using var demo = new ApiFactory(fixture.ConnectionString, "Demo");
        using var client = demo.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operationIds = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Select(operation => operation.Value.GetProperty("operationId").GetString())
            .ToList();
        Assert.Contains("GetDashboard", operationIds);
        Assert.Contains("GetCurrentUser", operationIds);
        Assert.True(document.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Keycloak", out _));
    }
}
