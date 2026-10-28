using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Subjects.ListSubjects;

public sealed class ListSubjectsValidator : AbstractValidator<ListSubjectsQuery>
{
    public ListSubjectsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Sort)
            .Must(sort => sort is null || ListSubjectsQuery.SortFields.Contains(sort, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Sort by one of: {string.Join(", ", ListSubjectsQuery.SortFields)}.");
    }
}
