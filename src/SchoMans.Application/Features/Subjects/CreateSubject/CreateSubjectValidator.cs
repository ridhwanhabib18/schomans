using FluentValidation;

namespace SchoMans.Application.Features.Subjects.CreateSubject;

public sealed class CreateSubjectValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MinimumPassingScore).InclusiveBetween(0m, 100m);
    }
}
