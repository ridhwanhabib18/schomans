using FluentValidation;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.RegenerateProgressSummary;

public sealed class RegenerateProgressSummaryValidator : AbstractValidator<RegenerateProgressSummaryCommand>
{
    public RegenerateProgressSummaryValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Month).Must(ProgressSummary.IsMonthKey).WithMessage("A month looks like 2026-10.");
    }
}
