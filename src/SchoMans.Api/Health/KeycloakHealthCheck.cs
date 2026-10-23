using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SchoMans.Api.Health;

/// <summary>Checks that Keycloak serves its OIDC metadata, which the API needs to validate tokens.</summary>
public sealed class KeycloakHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IHealthCheck
{
    public const string ClientName = "keycloak-health";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var metadataAddress = configuration["Keycloak:MetadataAddress"];
        if (string.IsNullOrWhiteSpace(metadataAddress))
        {
            metadataAddress = $"{configuration["Keycloak:Authority"]!.TrimEnd('/')}/.well-known/openid-configuration";
        }

        try
        {
            using var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(metadataAddress, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Keycloak answered {(int)response.StatusCode}.");
        }
        catch (HttpRequestException exception)
        {
            return HealthCheckResult.Unhealthy("Keycloak is not reachable.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Keycloak did not answer in time.", exception);
        }
    }
}
