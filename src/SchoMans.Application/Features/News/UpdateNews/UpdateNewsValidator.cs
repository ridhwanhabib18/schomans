using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.News.UpdateNews;

public sealed class UpdateNewsValidator : AbstractValidator<UpdateNewsCommand>
{
    public UpdateNewsValidator()
    {
        RuleFor(x => x.Title).MustBeTitle();
        RuleFor(x => x.Body).MustBeBody();
        RuleFor(x => x.Category).MustBeCategory();
        RuleFor(x => x.MetaDescription).MustBeMetaDescription();
        RuleFor(x => x.CoverImageKey).MaximumLength(300);
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
