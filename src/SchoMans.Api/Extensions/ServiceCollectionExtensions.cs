using SchoMans.Api.Endpoints;
using SchoMans.Api.Health;
using SchoMans.Api.Middleware;
using SchoMans.Api.OpenApi;

namespace SchoMans.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string CorsPolicy = "web";

    /// <summary>Everything the Api layer needs that is not authentication: CORS, ProblemDetails, OpenAPI, health checks.</summary>
    public static IServiceCollection AddSchoMansApi(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi(options => options.AddDocumentTransformer<KeycloakSecuritySchemeTransformer>());

        services.AddHttpClient(KeycloakHealthCheck.ClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("sqlserver", tags: [HealthEndpoints.ReadyTag])
            .AddCheck<KeycloakHealthCheck>("keycloak", tags: [HealthEndpoints.ReadyTag]);

        return services;
    }
}
