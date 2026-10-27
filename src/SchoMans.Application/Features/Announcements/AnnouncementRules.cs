using FluentValidation;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Announcements;

internal static class AnnouncementRules
{
    public const int MaxBodyLength = 100_000;

    public static IRuleBuilderOptions<T, string> MustBeTitle<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(200);

    public static IRuleBuilderOptions<T, string> MustBeBody<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(MaxBodyLength);

    public static IRuleBuilderOptions<T, IReadOnlyList<UserRole>> MustBeTargetRoles<T>(this IRuleBuilder<T, IReadOnlyList<UserRole>> rule) =>
        rule.NotNull().Must(roles => roles.All(Enum.IsDefined)).WithMessage("One of the target roles is not a valid role.");

    public static IRuleBuilderOptions<T, IReadOnlyList<Guid>> MustBeTargetClassrooms<T>(this IRuleBuilder<T, IReadOnlyList<Guid>> rule) =>
        rule.NotNull().Must(ids => ids.Count <= 50 && ids.All(id => id != Guid.Empty)).WithMessage("At most 50 classrooms can be targeted.");
}
