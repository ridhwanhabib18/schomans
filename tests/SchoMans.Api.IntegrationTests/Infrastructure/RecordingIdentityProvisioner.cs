using System.Collections.Concurrent;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for the Keycloak Admin API in the endpoint tests: it records whose role was changed, and a test can make it
/// fail to see that an enrollment then changes nothing. The real calls are covered by <c>KeycloakIdentityProvisionerTests</c>.
/// </summary>
public sealed class RecordingIdentityProvisioner : IIdentityProvisioner
{
    private readonly ConcurrentQueue<string> _promoted = new();

    public IReadOnlyCollection<string> Promoted => _promoted.ToArray();

    public bool Fail { get; set; }

    public Task<Result<Unit>> PromoteApplicantToStudentAsync(string keycloakUserId, CancellationToken ct)
    {
        if (Fail)
        {
            return Task.FromResult<Result<Unit>>(Error.Unavailable("admissions.identity_failed", "The identity provider is down."));
        }

        _promoted.Enqueue(keycloakUserId);
        return Task.FromResult<Result<Unit>>(new Unit());
    }
}
