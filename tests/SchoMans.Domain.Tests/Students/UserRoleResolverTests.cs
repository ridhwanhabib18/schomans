using SchoMans.Domain.Students;

namespace SchoMans.Domain.Tests.Students;

public class UserRoleResolverTests
{
    [Theory]
    [InlineData("Principal", UserRole.Principal)]
    [InlineData("teacher", UserRole.Teacher)]
    [InlineData("  STUDENT ", UserRole.Student)]
    [InlineData("Parent", UserRole.Parent)]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("Applicant", UserRole.Applicant)]
    public void Resolves_each_realm_role_name(string name, UserRole expected)
    {
        var found = UserRoleResolver.TryResolve([name], out var role);

        Assert.True(found);
        Assert.Equal(expected, role);
    }

    [Fact]
    public void Picks_the_most_privileged_role_when_a_token_carries_several()
    {
        // A freshly imported demo user can still carry the default Applicant role next to the real one.
        var found = UserRoleResolver.TryResolve(["Applicant", "Teacher", "offline_access"], out var role);

        Assert.True(found);
        Assert.Equal(UserRole.Teacher, role);
    }

    [Fact]
    public void Principal_wins_over_admin()
    {
        UserRoleResolver.TryResolve(["Admin", "Principal"], out var role);

        Assert.Equal(UserRole.Principal, role);
    }

    [Theory]
    [InlineData("offline_access")]
    [InlineData("uma_authorization")]
    [InlineData("default-roles-schomans")]
    [InlineData("Guest")]
    [InlineData("")]
    [InlineData("1")]
    public void Ignores_names_that_are_not_application_roles(string name)
    {
        // "1" would parse as UserRole.Teacher with Enum.TryParse, which must not grant a role.
        var found = UserRoleResolver.TryResolve([name], out _);

        Assert.False(found);
    }

    [Fact]
    public void Returns_false_for_no_roles()
    {
        Assert.False(UserRoleResolver.TryResolve([], out _));
    }
}
