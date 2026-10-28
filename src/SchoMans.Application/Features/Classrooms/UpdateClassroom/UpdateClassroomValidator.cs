using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Classrooms.UpdateClassroom;

public sealed class UpdateClassroomValidator : AbstractValidator<UpdateClassroomCommand>
{
    public UpdateClassroomValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.HomeroomTeacherId).NotEmpty();
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
