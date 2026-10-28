using FluentValidation;

namespace SchoMans.Application.Features.AcademicYears.CreateAcademicYear;

public sealed class CreateAcademicYearValidator : AbstractValidator<CreateAcademicYearCommand>
{
    public CreateAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("An academic year must end after it starts.");
        RuleFor(x => x.Terms).NotNull().Must(t => t is { Count: >= 1 and <= 4 }).WithMessage("An academic year has between 1 and 4 terms.");
        RuleForEach(x => x.Terms).SetValidator(new TermInputValidator());
    }
}
