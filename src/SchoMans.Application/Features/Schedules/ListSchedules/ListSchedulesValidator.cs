using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Schedules.ListSchedules;

public sealed class ListSchedulesValidator : AbstractValidator<ListSchedulesQuery>
{
    public ListSchedulesValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.DayOfWeek).IsInEnum();
    }
}
