using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.AcademicYears.ListAcademicYears;

public sealed class ListAcademicYearsValidator : AbstractValidator<ListAcademicYearsQuery>
{
    public ListAcademicYearsValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}
