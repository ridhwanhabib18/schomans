using FluentValidation;

namespace SchoMans.Application.Features.News.CreateNews;

public sealed class CreateNewsValidator : AbstractValidator<CreateNewsCommand>
{
    public CreateNewsValidator()
    {
        RuleFor(x => x.Title).MustBeTitle();
        RuleFor(x => x.Body).MustBeBody();
        RuleFor(x => x.Category).MustBeCategory();
        RuleFor(x => x.MetaDescription).MustBeMetaDescription();
    }
}
