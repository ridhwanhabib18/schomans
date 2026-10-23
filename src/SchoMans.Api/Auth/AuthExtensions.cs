using Microsoft.AspNetCore.Authentication.JwtBearer;
using SchoMans.Domain.Students;

namespace SchoMans.Api.Auth;

public static class AuthExtensions
{
    public const string AudienceKey = "Keycloak:Audience";

    /// <summary>JWT bearer authentication against Keycloak, and one policy per role.</summary>
    public static IServiceCollection AddSchoMansAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var authority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");
        var metadataAddress = configuration["Keycloak:MetadataAddress"];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                {
                    // Inside Docker the API reaches Keycloak by its service name while tokens carry the public issuer.
                    options.MetadataAddress = metadataAddress;
                }

                options.RequireHttpsMetadata = configuration.GetValue("Keycloak:RequireHttpsMetadata", true);
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidAudience = configuration[AudienceKey];
                options.TokenValidationParameters.ValidateAudience = true;
                options.Events = new JwtBearerEvents { OnTokenValidated = AddRoleClaim };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Principal, p => p.RequireRole(nameof(UserRole.Principal)))
            .AddPolicy(Policies.Teacher, p => p.RequireRole(nameof(UserRole.Teacher)))
            .AddPolicy(Policies.Student, p => p.RequireRole(nameof(UserRole.Student)))
            .AddPolicy(Policies.Parent, p => p.RequireRole(nameof(UserRole.Parent)))
            .AddPolicy(Policies.Admin, p => p.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(Policies.Applicant, p => p.RequireRole(nameof(UserRole.Applicant)))
            .AddPolicy(Policies.AnyRole, p => p.RequireRole(Enum.GetNames<UserRole>()));

        return services;
    }

    private static Task AddRoleClaim(TokenValidatedContext context)
    {
        if (context.Principal is not null)
        {
            RoleClaimMapper.AddRoleClaim(context.Principal);
        }

        return Task.CompletedTask;
    }
}
