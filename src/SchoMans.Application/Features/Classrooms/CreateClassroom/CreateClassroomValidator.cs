using FluentValidation;

namespace SchoMans.Application.Features.Classrooms.CreateClassroom;

public sealed class CreateClassroomValidator : AbstractValidator<CreateClassroomCommand>
{
    public CreateClassroomValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AcademicYearId).NotEmpty();
        RuleFor(x => x.HomeroomTeacherId).NotEmpty();
    }
}
