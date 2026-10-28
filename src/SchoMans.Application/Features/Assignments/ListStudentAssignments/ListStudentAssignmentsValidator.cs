using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Assignments.ListStudentAssignments;

public sealed class ListStudentAssignmentsValidator : AbstractValidator<ListStudentAssignmentsQuery>
{
    public ListStudentAssignmentsValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}
