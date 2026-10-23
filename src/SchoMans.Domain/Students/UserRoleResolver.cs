namespace SchoMans.Domain.Students;

/// <summary>Maps the realm role names found in a token to the single application role of a user.</summary>
public static class UserRoleResolver
{
    // Highest privilege first. A token can carry several roles (for example the default Applicant role
    // next to a real one); the most privileged one wins.
    private static readonly UserRole[] Priority =
    [
        UserRole.Principal,
        UserRole.Admin,
        UserRole.Teacher,
        UserRole.Parent,
        UserRole.Student,
        UserRole.Applicant,
    ];

    public static bool TryResolve(IEnumerable<string> roleNames, out UserRole role)
    {
        var known = new HashSet<UserRole>();
        foreach (var name in roleNames)
        {
            if (TryParse(name, out var parsed))
            {
                known.Add(parsed);
            }
        }

        foreach (var candidate in Priority)
        {
            if (known.Contains(candidate))
            {
                role = candidate;
                return true;
            }
        }

        role = default;
        return false;
    }

    // Enum.TryParse would also accept numeric strings such as "1", so match the names explicitly.
    private static bool TryParse(string? name, out UserRole role)
    {
        foreach (var candidate in Priority)
        {
            if (string.Equals(candidate.ToString(), name?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                role = candidate;
                return true;
            }
        }

        role = default;
        return false;
    }
}
