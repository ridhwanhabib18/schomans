using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SchoMans.Infrastructure.Identity;

namespace SchoMans.Api.IntegrationTests.Identity;

/// <summary>
/// The real provisioner against a scripted Keycloak: the calls it makes (token, the two roles, the mapping swap) and how it
/// fails. The endpoint tests replace it with a recording stand-in, so this is where its own code is exercised.
/// </summary>
public class KeycloakIdentityProvisionerTests
{
    private sealed record Call(HttpMethod Method, string Path, string? Authorization, string Body);

    private sealed class ScriptedKeycloak(Func<HttpRequestMessage, HttpStatusCode>? status = null) : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new Call(request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(), body));

            var code = status?.Invoke(request) ?? HttpStatusCode.OK;
            var json = request.RequestUri.AbsolutePath switch
            {
                var path when path.EndsWith("/token", StringComparison.Ordinal) => """{"access_token":"service-token"}""",
                var path when path.EndsWith("/roles/Student", StringComparison.Ordinal) => """{"id":"r-student","name":"Student"}""",
                var path when path.EndsWith("/roles/Applicant", StringComparison.Ordinal) => """{"id":"r-applicant","name":"Applicant"}""",
                _ => string.Empty,
            };
            return new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static KeycloakIdentityProvisioner Provisioner(HttpMessageHandler handler, string secret = "s3cret") => new(
        new HttpClient(handler),
        Options.Create(new KeycloakAdminOptions { BaseUrl = "http://keycloak.test/", Realm = "schomans", ClientId = "schomans-admin-api", ClientSecret = secret }),
        NullLogger<KeycloakIdentityProvisioner>.Instance);

    [Fact]
    public async Task It_signs_in_as_the_service_account_and_swaps_the_realm_roles()
    {
        var keycloak = new ScriptedKeycloak();

        var result = await Provisioner(keycloak).PromoteApplicantToStudentAsync("user-123", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                (HttpMethod.Post, "/realms/schomans/protocol/openid-connect/token"),
                (HttpMethod.Get, "/admin/realms/schomans/roles/Student"),
                (HttpMethod.Get, "/admin/realms/schomans/roles/Applicant"),
                (HttpMethod.Post, "/admin/realms/schomans/users/user-123/role-mappings/realm"),
                (HttpMethod.Delete, "/admin/realms/schomans/users/user-123/role-mappings/realm"),
            ],
            keycloak.Calls.Select(c => (c.Method, c.Path)));

        var token = keycloak.Calls[0].Body;
        Assert.Contains("grant_type=client_credentials", token);
        Assert.Contains("client_id=schomans-admin-api", token);
        Assert.Contains("client_secret=s3cret", token);
        Assert.All(keycloak.Calls.Skip(1), c => Assert.Equal("Bearer service-token", c.Authorization));
        Assert.Contains("\"name\":\"Student\"", keycloak.Calls[3].Body); // the Student role is added...
        Assert.Contains("\"name\":\"Applicant\"", keycloak.Calls[4].Body); // ...and the Applicant role is taken away
    }

    [Fact]
    public async Task An_account_id_with_special_characters_stays_inside_its_path()
    {
        var keycloak = new ScriptedKeycloak();

        await Provisioner(keycloak).PromoteApplicantToStudentAsync("a/b c", CancellationToken.None);

        Assert.Contains(keycloak.Calls, c => c.Method == HttpMethod.Post && c.Path.EndsWith("/users/a%2Fb%20c/role-mappings/realm", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/token")]
    [InlineData("/roles/Student")]
    [InlineData("/role-mappings/realm")]
    public async Task A_failing_call_is_reported_as_unavailable_and_stops_the_swap(string failingPath)
    {
        var keycloak = new ScriptedKeycloak(request => request.RequestUri!.AbsolutePath.EndsWith(failingPath, StringComparison.Ordinal) ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);

        var result = await Provisioner(keycloak).PromoteApplicantToStudentAsync("user-123", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(("admissions.identity_failed", SchoMans.Application.Common.Errors.ErrorType.Unavailable), (result.Error.Code, result.Error.Type));
        Assert.DoesNotContain(keycloak.Calls, c => c.Method == HttpMethod.Delete); // the Applicant role stays until the Student role was given
    }

    [Fact]
    public async Task Without_settings_it_does_not_call_keycloak_at_all()
    {
        var keycloak = new ScriptedKeycloak();

        var result = await Provisioner(keycloak, secret: "").PromoteApplicantToStudentAsync("user-123", CancellationToken.None);

        Assert.Equal("admissions.identity_not_configured", result.Error.Code);
        Assert.Empty(keycloak.Calls);
    }
}
