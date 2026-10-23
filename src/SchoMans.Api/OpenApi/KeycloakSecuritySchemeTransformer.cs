using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SchoMans.Api.OpenApi;

/// <summary>Adds the Keycloak OAuth2 (authorization code) security scheme to the OpenAPI document so the docs UI can sign in.</summary>
internal sealed class KeycloakSecuritySchemeTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Keycloak";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var authority = configuration["Keycloak:Authority"]!.TrimEnd('/');

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"{authority}/protocol/openid-connect/auth"),
                    TokenUrl = new Uri($"{authority}/protocol/openid-connect/token"),
                    Scopes = new Dictionary<string, string> { ["openid"] = "OpenID Connect" },
                },
            },
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        });

        return Task.CompletedTask;
    }
}
