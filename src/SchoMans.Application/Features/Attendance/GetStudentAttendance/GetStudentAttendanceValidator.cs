using FluentValidation;

namespace SchoMans.Application.Features.Attendance.GetStudentAttendance;

public sealed class GetStudentAttendanceValidator : AbstractValidator<GetStudentAttendanceQuery>
{
    public GetStudentAttendanceValidator() => RuleFor(x => x.Month).MustBeMonth();
}
