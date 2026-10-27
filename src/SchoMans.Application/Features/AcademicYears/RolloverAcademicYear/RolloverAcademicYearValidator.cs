using FluentValidation;

namespace SchoMans.Application.Features.AcademicYears.RolloverAcademicYear;

public sealed class RolloverAcademicYearValidator : AbstractValidator<RolloverAcademicYearCommand>
{
    public RolloverAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("An academic year must end after it starts.");
        RuleFor(x => x.Terms).NotNull().Must(t => t is { Count: >= 1 and <= 4 }).WithMessage("An academic year has between 1 and 4 terms.");
        RuleForEach(x => x.Terms).SetValidator(new TermInputValidator());
        RuleFor(x => x.Mappings).NotEmpty().WithMessage("Map every classroom of the current year.");
        RuleForEach(x => x.Mappings).ChildRules(mapping =>
        {
            mapping.RuleFor(m => m.FromClassroomId).NotEmpty();
            mapping.RuleFor(m => m.ToClassroomName)
                .NotEmpty().When(m => !m.Graduate).WithMessage("Name the new classroom, or mark this classroom as graduating.")
                .MaximumLength(50);
            mapping.RuleFor(m => m.HomeroomTeacherId)
                .NotEmpty().When(m => !m.Graduate).WithMessage("Choose the homeroom teacher of the new classroom.");
        });
    }
}
