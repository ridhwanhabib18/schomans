using FluentValidation;

namespace SchoMans.Application.Features.Attendance.OpenSession;

public sealed class OpenSessionValidator : AbstractValidator<OpenSessionCommand>
{
    public OpenSessionValidator() => RuleFor(x => x.ScheduleId).NotEmpty();
}
