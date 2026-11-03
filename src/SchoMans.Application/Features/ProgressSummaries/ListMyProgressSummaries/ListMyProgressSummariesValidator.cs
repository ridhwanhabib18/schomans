using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.ProgressSummaries.ListMyProgressSummaries;

public sealed class ListMyProgressSummariesValidator : AbstractValidator<ListMyProgressSummariesQuery>
{
    public ListMyProgressSummariesValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}
