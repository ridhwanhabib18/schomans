using FluentValidation;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.News;

internal static class NewsRules
{
    public const int MaxBodyLength = 100_000;

    public static IRuleBuilderOptions<T, string> MustBeTitle<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(200);

    public static IRuleBuilderOptions<T, string> MustBeBody<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(MaxBodyLength);

    public static IRuleBuilderOptions<T, NewsCategory> MustBeCategory<T>(this IRuleBuilder<T, NewsCategory> rule) =>
        rule.IsInEnum();

    public static IRuleBuilderOptions<T, string?> MustBeMetaDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(300);
}
