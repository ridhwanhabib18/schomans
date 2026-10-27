using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Teachers.CreateTeacher;

public sealed class CreateTeacherValidator : AbstractValidator<CreateTeacherCommand>
{
    public CreateTeacherValidator()
    {
        RuleFor(x => x.FullName).MustBePersonName();
        RuleFor(x => x.Email).MustBeEmail();
        RuleFor(x => x.Nip).MustBeNip();
    }
}
