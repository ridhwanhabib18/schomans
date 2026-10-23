using System.Security.Claims;
using System.Text.Json;

namespace SchoMans.Api.Auth;

/// <summary>Reads the claims of a Keycloak access token.</summary>
public static class KeycloakClaims
{
    public static IReadOnlyCollection<string> GetRealmRoles(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue("realm_access");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("roles", out var roles)
                || roles.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return roles.EnumerateArray()
                .Where(role => role.ValueKind == JsonValueKind.String)
                .Select(role => role.GetString()!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string? GetEmail(ClaimsPrincipal principal) => principal.FindFirstValue("email");

    /// <summary>The display name: the <c>name</c> claim, else the username, else the part of the email before the at sign.</summary>
    public static string? GetFullName(ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue("name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var username = principal.FindFirstValue("preferred_username");
        if (!string.IsNullOrWhiteSpace(username))
        {
            return username;
        }

        var email = GetEmail(principal);
        return email?.Split('@')[0];
    }
}
