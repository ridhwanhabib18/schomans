using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.TeacherNotes.ListTeacherNotes;

public sealed class ListTeacherNotesValidator : AbstractValidator<ListTeacherNotesQuery>
{
    public ListTeacherNotesValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        this.RuleForPaging(x => x.Page, x => x.PageSize);
    }
}
