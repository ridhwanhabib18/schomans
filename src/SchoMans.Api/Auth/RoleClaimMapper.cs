using System.Security.Claims;
using SchoMans.Domain.Students;

namespace SchoMans.Api.Auth;

public static class RoleClaimMapper
{
    /// <summary>
    /// Turns the Keycloak realm roles of a validated token into one role claim holding the highest-privilege SchoMans role.
    /// A token without any SchoMans role gets no role claim, so every role policy rejects it with 403.
    /// </summary>
    public static void AddRoleClaim(ClaimsPrincipal principal)
    {
        if (principal.Identity is ClaimsIdentity identity
            && UserRoleResolver.TryResolve(KeycloakClaims.GetRealmRoles(principal), out var role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
        }
    }
}
