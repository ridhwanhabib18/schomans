using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Identity;

/// <summary>Configuration section <c>KeycloakAdmin</c>: a confidential client with a service account that may manage role mappings.</summary>
public sealed class KeycloakAdminOptions
{
    public const string SectionName = "KeycloakAdmin";

    /// <summary>The base address of Keycloak, for example http://localhost:8080.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Realm { get; set; } = "schomans";

    public string ClientId { get; set; } = "schomans-admin-api";

    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Changes the realm roles of an account with the Keycloak Admin REST API, signed in as a service account (client
/// credentials). The service account has only the rights to read roles and to manage users, nothing else.
/// </summary>
public sealed class KeycloakIdentityProvisioner(HttpClient http, IOptions<KeycloakAdminOptions> options, ILogger<KeycloakIdentityProvisioner> logger) : IIdentityProvisioner
{
    private const string ApplicantRole = "Applicant";
    private const string StudentRole = "Student";

    private readonly KeycloakAdminOptions _options = options.Value;

    public async Task<Result<Unit>> PromoteApplicantToStudentAsync(string keycloakUserId, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            return Error.Failure("admissions.identity_not_configured", "The identity provider is not set up to change roles (KeycloakAdmin settings).");
        }

        try
        {
            var token = await GetTokenAsync(ct);
            var student = await GetRoleAsync(token, StudentRole, ct);
            var applicant = await GetRoleAsync(token, ApplicantRole, ct);

            var mappings = $"{_options.BaseUrl.TrimEnd('/')}/admin/realms/{_options.Realm}/users/{Uri.EscapeDataString(keycloakUserId)}/role-mappings/realm";
            await SendAsync(HttpMethod.Post, mappings, token, student, ct);
            await SendAsync(HttpMethod.Delete, mappings, token, applicant, ct);
            return new Unit();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogError(exception, "Changing the realm role of Keycloak user {UserId} failed", keycloakUserId);
            return Error.Failure("admissions.identity_failed", "The account could not be changed to a student in the identity provider. Try again.");
        }
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        using var response = await http.PostAsync(
            $"{_options.BaseUrl.TrimEnd('/')}/realms/{_options.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
            }),
            ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Keycloak returned no token.");
    }

    private async Task<JsonElement> GetRoleAsync(string token, string name, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/admin/realms/{_options.Realm}/roles/{Uri.EscapeDataString(name)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.Clone();
    }

    private async Task SendAsync(HttpMethod method, string url, string token, JsonElement role, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent($"[{role.GetRawText()}]", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
