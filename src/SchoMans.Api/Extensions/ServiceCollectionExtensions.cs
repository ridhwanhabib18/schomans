using System.Text.Json.Serialization;
using SchoMans.Api.Endpoints;
using SchoMans.Api.Health;
using SchoMans.Api.Hubs;
using SchoMans.Api.Middleware;
using SchoMans.Api.OpenApi;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string CorsPolicy = "web";

    /// <summary>Everything the Api layer needs that is not authentication: CORS, JSON, ProblemDetails, SignalR, rate limits, OpenAPI, health checks.</summary>
    public static IServiceCollection AddSchoMansApi(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials() // SignalR negotiates with credentials; the origin list above is explicit, never a wildcard
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

        // Enums travel as names ("Teacher", "Draft"), which is what the API contract and the generated client show.
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());

            // Numbers are numbers: without this the OpenAPI document says "integer or string" and the generated client types follow.
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddSignalR().AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSingleton<INotificationService, SignalRNotificationService>();
        services.AddSchoMansRateLimiting(configuration);

        services.AddOpenApi(options => options.AddDocumentTransformer<KeycloakSecuritySchemeTransformer>());

        services.AddHttpClient(KeycloakHealthCheck.ClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("sqlserver", tags: [HealthEndpoints.ReadyTag])
            .AddCheck<KeycloakHealthCheck>("keycloak", tags: [HealthEndpoints.ReadyTag]);

        return services;
    }
}
