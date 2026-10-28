using System.Globalization;
using FluentValidation;

namespace SchoMans.Application.Features.Attendance;

/// <summary>A month parameter in the form yyyy-MM, and the school dates it covers.</summary>
internal static class AttendanceMonth
{
    public const string Format = "yyyy-MM";

    public static bool TryParse(string? text, out DateOnly first, out DateOnly last)
    {
        first = last = default;
        if (!DateOnly.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            return false;
        }

        first = new DateOnly(month.Year, month.Month, 1);
        last = first.AddMonths(1).AddDays(-1);
        return true;
    }

    public static IRuleBuilderOptions<T, string> MustBeMonth<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(text => TryParse(text, out _, out _)).WithMessage("The month must look like 2026-10.");
}
