using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Assignments.ListAssignments;

public sealed class ListAssignmentsValidator : AbstractValidator<ListAssignmentsQuery>
{
    public ListAssignmentsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
