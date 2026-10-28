using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Schedules.UpdateSchedule;

public sealed class UpdateScheduleValidator : AbstractValidator<UpdateScheduleCommand>
{
    public UpdateScheduleValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.DayOfWeek).IsInEnum();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("The lesson must end after it starts.");
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
