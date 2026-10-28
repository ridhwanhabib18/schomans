using FluentValidation;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.GradeWeights.SetGradeWeights;

public sealed class SetGradeWeightsValidator : AbstractValidator<SetGradeWeightsCommand>
{
    public SetGradeWeightsValidator()
    {
        RuleFor(x => x.Weights).NotNull();
        RuleForEach(x => x.Weights).ChildRules(w =>
        {
            w.RuleFor(i => i.Component).IsInEnum();
            w.RuleFor(i => i.WeightPercent).InclusiveBetween(0m, 100m);
        });

        RuleFor(x => x.Weights)
            .Must(weights => weights.Select(w => w.Component).Distinct().Count() == Enum.GetValues<GradeComponent>().Length
                && weights.Count == Enum.GetValues<GradeComponent>().Length)
            .WithMessage("Give a weight for each of the four components, once.")
            .When(x => x.Weights is not null);

        RuleFor(x => x.Weights)
            .Must(weights => GradeCalculator.WeightsAreValid(weights.Select(w => w.WeightPercent)))
            .WithMessage("The weights add up to 100.")
            .When(x => x.Weights is not null);
    }
}
