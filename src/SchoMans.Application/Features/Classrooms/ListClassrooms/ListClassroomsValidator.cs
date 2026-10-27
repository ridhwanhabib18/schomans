using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Classrooms.ListClassrooms;

public sealed class ListClassroomsValidator : AbstractValidator<ListClassroomsQuery>
{
    public ListClassroomsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Sort)
            .Must(sort => sort is null || ListClassroomsQuery.SortFields.Contains(sort, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Sort by one of: {string.Join(", ", ListClassroomsQuery.SortFields)}.");
    }
}
