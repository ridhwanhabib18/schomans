using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

/// <summary>An account. Passwords live in Keycloak; this row holds the profile and the app role.</summary>
public sealed class User : BaseEntity
{
    private User()
    {
    }

    /// <summary>The Keycloak <c>sub</c>. Null until the person logs in for the first time.</summary>
    public string? KeycloakId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public string? PhotoKey { get; private set; }

    public static User Create(string email, string fullName, UserRole role, string? keycloakId = null)
    {
        var user = new User();
        user.SyncProfile(email, fullName, role);
        user.KeycloakId = string.IsNullOrWhiteSpace(keycloakId) ? null : keycloakId;
        return user;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Links a pre-created account (for example imported by staff) to its Keycloak identity.</summary>
    public void LinkKeycloak(string keycloakId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keycloakId);

        if (KeycloakId is not null && KeycloakId != keycloakId)
        {
            throw new InvalidOperationException("This user is already linked to a different Keycloak account.");
        }

        KeycloakId = keycloakId;
    }

    /// <summary>Keycloak is the authority for identity and role, so a login overwrites these values.</summary>
    public void SyncProfile(string email, string fullName, UserRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        Email = NormalizeEmail(email);
        FullName = fullName.Trim();
        Role = role;
    }
}
