using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The real API against a real SQL Server, with Keycloak token validation replaced by <see cref="TestAuthHandler"/> and
/// object storage replaced by <see cref="InMemoryFileStorage"/>. Everything else (handlers, SignalR, the sanitizer,
/// ClosedXML) is the production code.
/// </summary>
public sealed class ApiFactory(
    string connectionString,
    string environment = "Testing",
    IReadOnlyDictionary<string, string>? settings = null,
    InMemoryFileStorage? storage = null) : WebApplicationFactory<Program>
{
    public InMemoryFileStorage Storage { get; } = storage ?? new InMemoryFileStorage();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
        builder.UseSetting("Site:PublicBaseUrl", "https://school.test");

        // Never contacted: the test auth scheme replaces JWT validation, so no metadata is fetched.
        builder.UseSetting("Keycloak:Authority", "http://keycloak.invalid/realms/schomans");

        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

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

            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(Storage);
        });
    }
}
