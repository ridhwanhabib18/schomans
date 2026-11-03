using FluentValidation;
using SchoMans.Domain.Grading;

namespace SchoMans.Application.Features.LearningObjectives.CreateLearningObjective;

public sealed class CreateLearningObjectiveValidator : AbstractValidator<CreateLearningObjectiveCommand>
{
    public CreateLearningObjectiveValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(LearningObjective.CodeMaxLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(LearningObjective.DescriptionMaxLength);
    }
}
