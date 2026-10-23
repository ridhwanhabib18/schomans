using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Users.SyncUser;

namespace SchoMans.Application.Tests.Features;

public class SyncUserTests
{
    private static SyncUserCommand Valid() =>
        new("5b0d3f06-0000-0000-0000-000000000001", "teacher@schomans.demo", "Budi Santoso", ["Teacher"]);

    [Fact]
    public void Validator_accepts_a_complete_identity()
    {
        Assert.True(new SyncUserValidator().Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("", "a@b.test", "Name")]
    [InlineData("sub", "", "Name")]
    [InlineData("sub", "not-an-email", "Name")]
    [InlineData("sub", "a@b.test", "")]
    public void Validator_rejects_missing_or_malformed_claims(string sub, string email, string name)
    {
        var command = new SyncUserCommand(sub, email, name, ["Teacher"]);

        Assert.False(new SyncUserValidator().Validate(command).IsValid);
    }

    [Fact]
    public async Task Handler_forbids_a_token_without_any_application_role()
    {
        // The role check comes first, so the database is never touched.
        var handler = new SyncUserHandler(null!);
        var command = Valid() with { RealmRoles = ["offline_access", "uma_authorization"] };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("auth.no_application_role", result.Error.Code);
    }
}
