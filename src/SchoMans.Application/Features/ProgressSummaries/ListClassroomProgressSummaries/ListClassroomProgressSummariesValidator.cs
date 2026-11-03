using FluentValidation;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.ListClassroomProgressSummaries;

public sealed class ListClassroomProgressSummariesValidator : AbstractValidator<ListClassroomProgressSummariesQuery>
{
    public ListClassroomProgressSummariesValidator()
    {
        RuleFor(x => x.ClassroomId).NotEmpty();
        RuleFor(x => x.Month).Must(m => m is null || ProgressSummary.IsMonthKey(m)).WithMessage("A month looks like 2026-10.");
    }
}
