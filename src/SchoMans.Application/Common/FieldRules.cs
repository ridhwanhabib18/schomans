using System.Text.RegularExpressions;
using FluentValidation;

namespace SchoMans.Application.Common;

/// <summary>Validation rules shared by several use cases (the same rules apply to a form and to a row of the Excel import).</summary>
public static partial class FieldRules
{
    public const int NisnLength = 10;
    public const int NipLength = 18;

    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex NisnPattern();

    [GeneratedRegex(@"^\d{18}$")]
    private static partial Regex NipPattern();

    public static IRuleBuilderOptions<T, string> MustBeNisn<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(value => NisnPattern().IsMatch(value ?? string.Empty)).WithMessage("The NISN must be exactly 10 digits.");

    public static IRuleBuilderOptions<T, string> MustBeNip<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(value => NipPattern().IsMatch(value ?? string.Empty)).WithMessage("The NIP must be exactly 18 digits.");

    public static IRuleBuilderOptions<T, string> MustBeEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(256);

    public static IRuleBuilderOptions<T, string> MustBePersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(200);

    public static IRuleBuilderOptions<T, string> MustBeRowVersion<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(value => RowVersionCodec.TryDecode(value, out _)).WithMessage("The row version is missing or not valid.");
}
