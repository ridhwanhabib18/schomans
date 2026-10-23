using SchoMans.Application.Features.Dashboard.GetDashboard;
using SchoMans.Application.Tests.TestDoubles;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Tests.Features;

public class GetDashboardHandlerTests
{
    [Theory]
    [InlineData(UserRole.Principal)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Parent)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Applicant)]
    public async Task Returns_an_empty_dashboard_for_the_role_of_the_current_user(UserRole role)
    {
        var handler = new GetDashboardHandler(new FakeCurrentUser(role, "Rina Putri"));

        var result = await handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(role.ToString(), result.Value.Role);
        Assert.Equal("Rina Putri", result.Value.FullName);
        Assert.Empty(result.Value.Sections);
    }
}
