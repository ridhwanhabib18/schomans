using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Teachers.UpdateTeacher;

public sealed class UpdateTeacherValidator : AbstractValidator<UpdateTeacherCommand>
{
    public UpdateTeacherValidator()
    {
        RuleFor(x => x.FullName).MustBePersonName();
        RuleFor(x => x.Email).MustBeEmail();
        RuleFor(x => x.Nip).MustBeNip();
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
