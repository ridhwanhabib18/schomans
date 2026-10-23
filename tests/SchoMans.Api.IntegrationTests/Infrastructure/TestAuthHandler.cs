using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoMans.Api.Auth;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for Keycloak JWT validation in the tests. It builds the same claims a Keycloak access token carries
/// (<c>sub</c>, <c>email</c>, <c>name</c>, <c>realm_access</c>) from request headers and runs the production
/// <see cref="RoleClaimMapper"/>, so the role policies, the user sync and the handlers are the real ones.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string SubHeader = "X-Test-Sub";
    public const string EmailHeader = "X-Test-Email";
    public const string NameHeader = "X-Test-Name";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubHeader, out var sub))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var roles = Request.Headers[RolesHeader].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new("sub", sub.ToString()),
            new("realm_access", JsonSerializer.Serialize(new { roles })),
        };
        AddIfPresent(claims, "email", EmailHeader);
        AddIfPresent(claims, "name", NameHeader);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        RoleClaimMapper.AddRoleClaim(principal);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    private void AddIfPresent(List<Claim> claims, string claimType, string header)
    {
        if (Request.Headers.TryGetValue(header, out var value) && !string.IsNullOrEmpty(value))
        {
            claims.Add(new Claim(claimType, value.ToString()));
        }
    }
}
