namespace SchoMans.Api.Endpoints;

public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    /// <summary><c>/health/live</c> only says the process runs; <c>/health/ready</c> also checks SQL Server and Keycloak.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains(ReadyTag) }).AllowAnonymous();
        return app;
    }
}
