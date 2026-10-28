using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.AcademicYears.UpdateAcademicYear;

public sealed class UpdateAcademicYearValidator : AbstractValidator<UpdateAcademicYearCommand>
{
    public UpdateAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("An academic year must end after it starts.");
        RuleFor(x => x.Terms).NotNull().Must(t => t is { Count: >= 1 and <= 4 }).WithMessage("An academic year has between 1 and 4 terms.");
        RuleForEach(x => x.Terms).SetValidator(new TermInputValidator());
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
