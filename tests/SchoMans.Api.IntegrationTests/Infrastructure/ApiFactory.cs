using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>The real API against a real SQL Server, with Keycloak token validation replaced by <see cref="TestAuthHandler"/>.</summary>
public sealed class ApiFactory(string connectionString, string environment = "Testing") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");

        // Never contacted: the test auth scheme replaces JWT validation, so no metadata is fetched.
        builder.UseSetting("Keycloak:Authority", "http://keycloak.invalid/realms/schomans");

        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
