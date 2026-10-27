using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Tests.Common;

public class RowVersionAndRulesTests
{
    [Fact]
    public void A_row_version_survives_the_trip_to_the_client_and_back()
    {
        byte[] original = [0, 0, 0, 0, 0, 0, 7, 209];

        Assert.True(RowVersionCodec.TryDecode(RowVersionCodec.Encode(original), out var decoded));
        Assert.Equal(original, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64!!")]
    public void A_missing_or_broken_row_version_is_rejected(string? text)
    {
        Assert.False(RowVersionCodec.TryDecode(text, out _));
    }

    private sealed record Sample(string Nisn, string Nip, string Email, string Name, string Version);

    private sealed class SampleValidator : AbstractValidator<Sample>
    {
        public SampleValidator()
        {
            RuleFor(x => x.Nisn).MustBeNisn();
            RuleFor(x => x.Nip).MustBeNip();
            RuleFor(x => x.Email).MustBeEmail();
            RuleFor(x => x.Name).MustBePersonName();
            RuleFor(x => x.Version).MustBeRowVersion();
        }
    }

    private static Sample Valid() => new("1234567890", "198001012005011001", "a@b.test", "A B", "AAAAAAAAB9E=");

    [Fact]
    public void Valid_values_pass_every_shared_rule()
    {
        Assert.True(new SampleValidator().Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("12345abcde")]
    [InlineData("")]
    public void An_nisn_is_exactly_ten_digits(string nisn)
    {
        Assert.False(new SampleValidator().Validate(Valid() with { Nisn = nisn }).IsValid);
    }

    [Theory]
    [InlineData("19800101200501100")]
    [InlineData("1980010120050110011")]
    [InlineData("19800101200501100x")]
    public void A_nip_is_exactly_eighteen_digits(string nip)
    {
        Assert.False(new SampleValidator().Validate(Valid() with { Nip = nip }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void An_email_must_be_an_email(string email)
    {
        Assert.False(new SampleValidator().Validate(Valid() with { Email = email }).IsValid);
    }

    [Fact]
    public void A_name_is_required_and_has_a_maximum_length()
    {
        Assert.False(new SampleValidator().Validate(Valid() with { Name = " " }).IsValid);
        Assert.False(new SampleValidator().Validate(Valid() with { Name = new string('x', 201) }).IsValid);
    }
}
