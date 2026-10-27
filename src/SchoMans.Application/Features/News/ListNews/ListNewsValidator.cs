using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.News.ListNews;

public sealed class ListNewsValidator : AbstractValidator<ListNewsQuery>
{
    public ListNewsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
