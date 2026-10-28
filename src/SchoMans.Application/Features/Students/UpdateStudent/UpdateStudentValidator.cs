using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Students.UpdateStudent;

public sealed class UpdateStudentValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentValidator()
    {
        RuleFor(x => x.FullName).MustBePersonName();
        RuleFor(x => x.Email).MustBeEmail();
        RuleFor(x => x.Nisn).MustBeNisn();
        RuleFor(x => x.ClassroomId).NotEmpty();
        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow.Date)).WithMessage("The date of birth must be in the past.")
            .GreaterThan(new DateOnly(1980, 1, 1)).WithMessage("The date of birth is not plausible.");
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
