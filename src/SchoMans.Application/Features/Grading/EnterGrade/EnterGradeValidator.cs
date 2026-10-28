using FluentValidation;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.Grading.EnterGrade;

public sealed class EnterGradeValidator : AbstractValidator<EnterGradeCommand>
{
    public EnterGradeValidator()
    {
        RuleFor(x => x.Component).IsInEnum().NotEqual(GradeComponent.Assignment)
            .WithMessage("Assignment scores come from the scored assignments.");
        RuleFor(x => x.Score).InclusiveBetween(0m, 100m);
        RuleFor(x => x.Score).Must(score => decimal.Round(score, 2) == score).WithMessage("A score has at most two decimals.");
    }
}
