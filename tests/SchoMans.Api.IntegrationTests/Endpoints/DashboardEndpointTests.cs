using System.Net;
using System.Net.Http.Json;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Dashboard.GetDashboard;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class DashboardEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/dashboard";

    [Theory]
    [InlineData("Principal")]
    [InlineData("Teacher")]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Admin")]
    [InlineData("Applicant")]
    public async Task Every_role_gets_its_own_empty_dashboard(string role)
    {
        using var client = fixture.AsNewUser(role);

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardDto>();
        Assert.NotNull(dashboard);
        Assert.Equal(role, dashboard.Role);
        Assert.Empty(dashboard.Sections);
    }

    [Fact]
    public async Task Highest_privilege_role_wins_when_the_token_carries_several()
    {
        using var client = fixture.AsNewUser("Applicant", "Teacher", "offline_access");

        var dashboard = await client.GetFromJsonAsync<DashboardDto>(Url);

        Assert.Equal("Teacher", dashboard!.Role);
    }

    [Fact]
    public async Task Request_without_a_token_is_unauthorized()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Signed_in_user_without_a_schomans_role_is_forbidden()
    {
        // A Keycloak account that only has the default realm roles must not get in.
        using var client = fixture.AsNewUser("offline_access", "uma_authorization", "default-roles-schomans");

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Token_without_an_email_claim_is_rejected_as_incomplete()
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "sub-no-email");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Teacher");

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("auth.incomplete_token", problem, StringComparison.Ordinal);
    }
}
