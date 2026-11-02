using System.Net;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Features.Requests;
using SchoMans.Application.Features.Requests.Chains;
using SchoMans.Application.Features.Requests.CreateRequest;
using SchoMans.Application.Features.Students.ListMyChildren;
using SchoMans.Domain.Approvals;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>The chains are global, so these tests have a database of their own: changing one must not disturb the request tests.</summary>
public class ApprovalChainEndpointTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Url = "/api/v1/approval-chains";

    private static async Task<List<ApprovalChainDto>> ChainsAsync(HttpClient client) =>
        await (await client.GetAsync(Url)).ReadAsync<List<ApprovalChainDto>>();

    [Fact]
    public async Task A_school_starts_with_the_default_chains_and_admin_and_principal_read_them()
    {
        using var admin = fixture.AsAdmin();
        using var principal = fixture.AsPrincipal();

        var chains = await ChainsAsync(admin);
        var seenByPrincipal = await ChainsAsync(principal);

        Assert.Equal([RequestType.StudentLeave, RequestType.TeacherLeave, RequestType.CertificateLetter], chains.Select(c => c.Type));
        Assert.Equal([ApproverRole.HomeroomTeacher, ApproverRole.Principal], chains.Single(c => c.Type == RequestType.StudentLeave).Roles);
        Assert.Equal([ApproverRole.Principal], chains.Single(c => c.Type == RequestType.TeacherLeave).Roles);
        Assert.Equal([ApproverRole.Admin, ApproverRole.Principal], chains.Single(c => c.Type == RequestType.CertificateLetter).Roles);
        Assert.Equal(chains.Select(c => c.Roles), seenByPrincipal.Select(c => c.Roles));
    }

    [Fact]
    public async Task The_admin_changes_a_chain_and_new_requests_follow_it()
    {
        using var admin = fixture.AsAdmin();
        using var parent = fixture.AsParent();
        var rina = (await (await parent.GetAsync("/api/v1/users/me/children")).ReadAsync<List<ChildDto>>()).Single(c => c.FullName == "Rina Putri");

        var changed = await admin.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.CertificateLetter, [ApproverRole.Principal]));
        var after = (await changed.ReadAsync<List<ApprovalChainDto>>()).Single(c => c.Type == RequestType.CertificateLetter);
        var request = await (await parent.PostJsonAsync("/api/v1/requests", new CreateRequestCommand(RequestType.CertificateLetter, rina.StudentId, null, null, "A letter"))).ReadAsync<RequestDetailDto>();

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal([ApproverRole.Principal], after.Roles);
        Assert.Equal([ApproverRole.Principal], request.Chain);
        Assert.Equal(ApproverRole.Principal, request.WaitingFor);

        // Back to the default for the tests that follow, once nothing waits any more.
        using var principal = fixture.AsPrincipal();
        (await principal.PostJsonAsync($"/api/v1/requests/{request.Id}/decision", new SchoMans.Application.Features.Requests.DecideRequest.DecideRequestCommand(request.Id, false, "Test over"))).EnsureSuccessStatusCode();
        (await admin.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.CertificateLetter, [ApproverRole.Admin, ApproverRole.Principal]))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_chain_cannot_change_while_requests_of_its_kind_wait_for_a_decision()
    {
        using var admin = fixture.AsAdmin();
        using var teacher = fixture.AsTeacher();
        using var principal = fixture.AsPrincipal();
        var request = await (await teacher.PostJsonAsync("/api/v1/requests", new CreateRequestCommand(
            RequestType.TeacherLeave, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)), "Training"))).ReadAsync<RequestDetailDto>();

        var blocked = await admin.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.TeacherLeave, [ApproverRole.Admin]));
        var listed = (await ChainsAsync(admin)).Single(c => c.Type == RequestType.TeacherLeave);
        var otherKind = await admin.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.StudentLeave, [ApproverRole.Principal]));

        Assert.Equal((HttpStatusCode.Conflict, "requests.chain_in_use"), (blocked.StatusCode, await blocked.ProblemCodeAsync()));
        Assert.True(listed.HasPendingRequests);
        Assert.Equal([ApproverRole.Principal], listed.Roles);
        Assert.Equal(HttpStatusCode.OK, otherKind.StatusCode); // only the kind with waiting requests is locked

        (await principal.PostJsonAsync($"/api/v1/requests/{request.Id}/decision", new SchoMans.Application.Features.Requests.DecideRequest.DecideRequestCommand(request.Id, true, null))).EnsureSuccessStatusCode();
        (await admin.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.StudentLeave, [ApproverRole.HomeroomTeacher, ApproverRole.Principal]))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_chain_is_one_to_five_steps_and_a_teacher_s_leave_has_no_homeroom_step()
    {
        using var admin = fixture.AsAdmin();

        var empty = await admin.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.StudentLeave, []));
        var six = await admin.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.StudentLeave, Enumerable.Repeat(ApproverRole.Admin, 6).ToList()));
        var homeroom = await admin.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.TeacherLeave, [ApproverRole.HomeroomTeacher]));
        var unchanged = (await ChainsAsync(admin)).Single(c => c.Type == RequestType.StudentLeave);

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, six.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, homeroom.StatusCode);
        Assert.Equal([ApproverRole.HomeroomTeacher, ApproverRole.Principal], unchanged.Roles);
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Student")]
    [InlineData("Parent")]
    [InlineData("Applicant")]
    public async Task Only_admin_and_principal_read_the_chains(string role)
    {
        using var client = fixture.AsNewUser(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.StudentLeave, [ApproverRole.Admin]))).StatusCode);
    }

    [Fact]
    public async Task The_principal_reads_but_does_not_change_the_chains()
    {
        using var principal = fixture.AsPrincipal();

        var response = await principal.PutJsonAsync(Url, new SetApprovalChainCommand(RequestType.StudentLeave, [ApproverRole.Principal]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
