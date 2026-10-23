using SchoMans.Domain.Students;

namespace SchoMans.Domain.Tests.Students;

public class UserTests
{
    [Fact]
    public void Create_normalizes_email_and_trims_name()
    {
        var user = User.Create("  Rina.Putri@Schomans.Demo ", "  Rina Putri ", UserRole.Student);

        Assert.Equal("rina.putri@schomans.demo", user.Email);
        Assert.Equal("Rina Putri", user.FullName);
        Assert.Null(user.KeycloakId);
    }

    [Fact]
    public void Create_with_keycloak_id_links_the_account()
    {
        var user = User.Create("a@b.test", "A B", UserRole.Applicant, "sub-1");

        Assert.Equal("sub-1", user.KeycloakId);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("a@b.test", " ")]
    public void Create_rejects_missing_email_or_name(string email, string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => User.Create(email, name, UserRole.Student));
    }

    [Fact]
    public void LinkKeycloak_is_idempotent_for_the_same_sub()
    {
        var user = User.Create("a@b.test", "A B", UserRole.Student);

        user.LinkKeycloak("sub-1");
        user.LinkKeycloak("sub-1");

        Assert.Equal("sub-1", user.KeycloakId);
    }

    [Fact]
    public void LinkKeycloak_refuses_to_move_an_account_to_another_identity()
    {
        var user = User.Create("a@b.test", "A B", UserRole.Student, "sub-1");

        Assert.Throws<InvalidOperationException>(() => user.LinkKeycloak("sub-2"));
    }

    [Fact]
    public void SyncProfile_overwrites_email_name_and_role()
    {
        var user = User.Create("old@b.test", "Old Name", UserRole.Applicant);

        user.SyncProfile("New@B.test", "New Name", UserRole.Student);

        Assert.Equal("new@b.test", user.Email);
        Assert.Equal("New Name", user.FullName);
        Assert.Equal(UserRole.Student, user.Role);
    }

    [Fact]
    public void MarkDeleted_flags_the_user_as_soft_deleted()
    {
        var user = User.Create("a@b.test", "A B", UserRole.Student);

        user.MarkDeleted();

        Assert.True(user.IsDeleted);
    }
}
