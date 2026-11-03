using FluentValidation;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.LearningObjectives.UpdateLearningObjective;

public sealed class UpdateLearningObjectiveValidator : AbstractValidator<UpdateLearningObjectiveCommand>
{
    public UpdateLearningObjectiveValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(LearningObjective.CodeMaxLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(LearningObjective.DescriptionMaxLength);
    }
}
