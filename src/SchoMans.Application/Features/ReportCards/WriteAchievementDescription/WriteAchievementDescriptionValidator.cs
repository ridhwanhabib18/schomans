using FluentValidation;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.ReportCards.WriteAchievementDescription;

public sealed class WriteAchievementDescriptionValidator : AbstractValidator<WriteAchievementDescriptionCommand>
{
    public WriteAchievementDescriptionValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(AchievementDescription.BodyMaxLength);
    }
}
