namespace SchoMans.Domain.Students;

/// <summary>
/// Keycloak realm roles. A Guest is an anonymous visitor and has no role, so it is not listed here.
/// </summary>
public enum UserRole
{
    Principal,
    Teacher,
    Student,
    Parent,
    Admin,
    Applicant,
}
