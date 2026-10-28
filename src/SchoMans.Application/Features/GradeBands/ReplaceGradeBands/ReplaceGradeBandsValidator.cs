using FluentValidation;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.GradeBands.ReplaceGradeBands;

public sealed class ReplaceGradeBandsValidator : AbstractValidator<ReplaceGradeBandsCommand>
{
    public const int MaxBands = 10;

    public ReplaceGradeBandsValidator()
    {
        RuleFor(x => x.Bands).NotNull().Must(b => b is { Count: > 0 and <= MaxBands }).WithMessage($"Give between 1 and {MaxBands} bands.");

        RuleForEach(x => x.Bands).ChildRules(band =>
        {
            band.RuleFor(b => b.Label).NotEmpty().MaximumLength(GradeBand.LabelMaxLength);
            band.RuleFor(b => b.MinScore).InclusiveBetween(0m, 100m);
        });

        RuleFor(x => x.Bands)
            .Must(bands => bands.Select(b => b.Label.Trim().ToUpperInvariant()).Distinct().Count() == bands.Count)
            .WithMessage("Every band has its own label.")
            .When(x => x.Bands is not null && x.Bands.All(b => b.Label is not null));

        RuleFor(x => x.Bands)
            .Must(bands => bands.Select(b => b.MinScore).Distinct().Count() == bands.Count)
            .WithMessage("Every band starts at its own score.")
            .When(x => x.Bands is not null);

        RuleFor(x => x.Bands)
            .Must(bands => bands.Any(b => b.MinScore == 0m))
            .WithMessage("One band must start at 0, so every grade gets a label.")
            .When(x => x.Bands is not null);
    }
}
