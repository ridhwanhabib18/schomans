namespace SchoMans.Api.IntegrationTests.Infrastructure;

public static class HttpClientExtensions
{
    /// <summary>A client that behaves as a signed-in Keycloak user with the given realm roles.</summary>
    public static HttpClient AsUser(this ApiFixture fixture, string email, string name, string sub, params string[] roles)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, name);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    /// <summary>A signed-in user with a fresh <c>sub</c> and email, so tests never collide on the shared database.</summary>
    public static HttpClient AsNewUser(this ApiFixture fixture, params string[] roles)
    {
        var id = Guid.NewGuid().ToString("N");
        return fixture.AsUser($"user-{id}@test.example", $"Test User {id[..6]}", $"sub-{id}", roles);
    }
}
