using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Students.ListStudents;

public sealed class ListStudentsValidator : AbstractValidator<ListStudentsQuery>
{
    public ListStudentsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Sort)
            .Must(sort => sort is null || ListStudentsQuery.SortFields.Contains(sort, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Sort by one of: {string.Join(", ", ListStudentsQuery.SortFields)}.");
    }
}
