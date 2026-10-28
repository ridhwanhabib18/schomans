using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

public static class TestJson
{
    /// <summary>The same settings as the API: web defaults, enums as names.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}

public static class HttpClientExtensions
{
    /// <summary>A client that behaves as a signed-in Keycloak user with the given realm roles.</summary>
    public static HttpClient AsUser(this ApiFixture fixture, string email, string name, string sub, params string[] roles) =>
        fixture.Factory.AsUser(email, name, sub, roles);

    public static HttpClient AsUser(this ApiFactory factory, string email, string name, string sub, params string[] roles)
    {
        var client = factory.CreateClient();
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

    // The seeded demo accounts. The first request of each links the account to its fixed sub (see SyncUserHandler).
    public static HttpClient AsAdmin(this ApiFixture fixture) =>
        fixture.AsUser("admin@schomans.demo", "Sari Utami", "sub-demo-admin", "Admin");

    public static HttpClient AsPrincipal(this ApiFixture fixture) =>
        fixture.AsUser("principal@schomans.demo", "Hendra Wirawan", "sub-demo-principal", "Principal");

    public static HttpClient AsTeacher(this ApiFixture fixture) =>
        fixture.AsUser("teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");

    public static HttpClient AsStudent(this ApiFixture fixture) =>
        fixture.AsUser("student@schomans.demo", "Rina Putri", "sub-demo-student", "Student");

    public static HttpClient AsParent(this ApiFixture fixture) =>
        fixture.AsUser("parent@schomans.demo", "Agus Putra", "sub-demo-parent", "Parent");

    public static HttpClient AsApplicant(this ApiFixture fixture) =>
        fixture.AsUser("applicant@schomans.demo", "Dewi Lestari", "sub-demo-applicant", "Applicant");

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, TestJson.Options);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, TestJson.Options);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(text, TestJson.Options)
            ?? throw new InvalidOperationException($"The response ({(int)response.StatusCode}) had no body: {text}");
    }

    /// <summary>The stable <c>code</c> of a ProblemDetails response (for example <c>students.nisn_taken</c>).</summary>
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
