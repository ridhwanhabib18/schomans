using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Subjects.UpdateSubject;

public sealed class UpdateSubjectValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MinimumPassingScore).InclusiveBetween(0m, 100m);
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
