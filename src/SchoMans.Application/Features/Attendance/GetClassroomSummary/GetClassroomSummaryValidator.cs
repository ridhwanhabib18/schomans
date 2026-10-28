using FluentValidation;

namespace SchoMans.Application.Features.Attendance.GetClassroomSummary;

public sealed class GetClassroomSummaryValidator : AbstractValidator<GetClassroomSummaryQuery>
{
    public GetClassroomSummaryValidator()
    {
        RuleFor(x => x.ClassroomId).NotEmpty();
        RuleFor(x => x.Month).MustBeMonth();
    }
}
