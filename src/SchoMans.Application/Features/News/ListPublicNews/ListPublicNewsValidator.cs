using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.News.ListPublicNews;

public sealed class ListPublicNewsValidator : AbstractValidator<ListPublicNewsQuery>
{
    public ListPublicNewsValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}
