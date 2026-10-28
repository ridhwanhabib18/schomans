using FluentValidation;
using SchoMans.Domain.Attendance;

namespace SchoMans.Application.Features.Attendance.MarkAttendance;

public sealed class MarkAttendanceValidator : AbstractValidator<MarkAttendanceCommand>
{
    public MarkAttendanceValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(AttendanceRecord.NoteMaxLength);
    }
}
