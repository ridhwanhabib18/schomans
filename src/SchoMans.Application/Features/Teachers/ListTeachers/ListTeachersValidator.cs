using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Teachers.ListTeachers;

public sealed class ListTeachersValidator : AbstractValidator<ListTeachersQuery>
{
    public ListTeachersValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Sort)
            .Must(sort => sort is null || ListTeachersQuery.SortFields.Contains(sort, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Sort by one of: {string.Join(", ", ListTeachersQuery.SortFields)}.");
    }
}
