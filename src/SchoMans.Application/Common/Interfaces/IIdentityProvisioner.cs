using SchoMans.Application.Common.Errors;

namespace SchoMans.Application.Common.Interfaces;

/// <summary>
/// Changes accounts in the identity provider (Keycloak). The realm role is the authority of a person's role, so an
/// enrolled applicant has to become a student there, not only in the database.
/// </summary>
public interface IIdentityProvisioner
{
    /// <summary>Gives the account the realm role Student and takes the realm role Applicant away. Doing it twice changes nothing.</summary>
    Task<Result<Unit>> PromoteApplicantToStudentAsync(string keycloakUserId, CancellationToken ct);
}
