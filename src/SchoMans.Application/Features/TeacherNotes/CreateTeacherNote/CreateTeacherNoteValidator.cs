using FluentValidation;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.TeacherNotes.CreateTeacherNote;

public sealed class CreateTeacherNoteValidator : AbstractValidator<CreateTeacherNoteCommand>
{
    public CreateTeacherNoteValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(TeacherNote.BodyMaxLength);
    }
}
