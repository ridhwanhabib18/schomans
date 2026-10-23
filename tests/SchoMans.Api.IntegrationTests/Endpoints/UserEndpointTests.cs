using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Users.GetMe;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public class UserEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/users/me";

    [Fact]
    public async Task Self_registered_applicant_is_created_on_the_first_request()
    {
        using var client = fixture.AsUser("new.applicant@test.example", "New Applicant", "sub-new-applicant", "Applicant");

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeDto>();
        Assert.Equal("new.applicant@test.example", me!.Email);
        Assert.Equal("New Applicant", me.FullName);
        Assert.Equal("Applicant", me.Role);
        Assert.False(me.IsHomeroomTeacher);
    }

    [Fact]
    public async Task Seeded_teacher_is_linked_on_first_login_and_is_a_homeroom_teacher()
    {
        using var client = fixture.AsUser("teacher@schomans.demo", "Budi Santoso", "sub-demo-teacher", "Teacher");

        var me = await client.GetFromJsonAsync<MeDto>(Url);

        Assert.Equal("Teacher", me!.Role);
        Assert.True(me.IsHomeroomTeacher);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "teacher@schomans.demo");
        Assert.Equal("sub-demo-teacher", user.KeycloakId);
        Assert.Equal(user.Id, me.Id);
    }

    [Fact]
    public async Task Email_already_linked_to_another_keycloak_account_is_a_conflict()
    {
        using var first = fixture.AsUser("admin@schomans.demo", "Sari Utami", "sub-demo-admin", "Admin");
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync(Url)).StatusCode);

        using var impostor = fixture.AsUser("admin@schomans.demo", "Someone Else", "sub-impostor", "Admin");
        var response = await impostor.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("auth.email_already_linked", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Role_and_name_changes_in_keycloak_are_synced_on_the_next_request()
    {
        const string email = "promoted@test.example";
        using var asApplicant = fixture.AsUser(email, "Old Name", "sub-promoted", "Applicant");
        Assert.Equal("Applicant", (await asApplicant.GetFromJsonAsync<MeDto>(Url))!.Role);

        using var asStudent = fixture.AsUser(email, "New Name", "sub-promoted", "Student");
        var me = await asStudent.GetFromJsonAsync<MeDto>(Url);

        Assert.Equal("Student", me!.Role);
        Assert.Equal("New Name", me.FullName);
    }

    [Fact]
    public async Task Repeated_and_parallel_first_requests_create_exactly_one_user()
    {
        // Several rounds, each with a brand-new user, so the race on the unique indexes is really exercised.
        for (var round = 0; round < 5; round++)
        {
            var email = $"parallel-{round}-{Guid.NewGuid():N}@test.example";
            using var client = fixture.AsUser(email, "Parallel User", $"sub-parallel-{round}-{Guid.NewGuid():N}", "Parent");

            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.GetAsync(Url)));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.Users.CountAsync(u => u.Email == email));
        }
    }

    [Fact]
    public async Task Request_without_a_token_is_unauthorized()
    {
        using var client = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Signed_in_user_without_a_schomans_role_is_forbidden()
    {
        using var client = fixture.AsNewUser("offline_access");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }
}
