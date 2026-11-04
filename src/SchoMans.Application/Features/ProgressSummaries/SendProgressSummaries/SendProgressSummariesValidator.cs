using FluentValidation;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.SendProgressSummaries;

public sealed class SendProgressSummariesValidator : AbstractValidator<SendProgressSummariesCommand>
{
    public SendProgressSummariesValidator()
    {
        RuleFor(x => x.ClassroomId).NotEmpty();
        RuleFor(x => x.Month).Must(ProgressSummary.IsMonthKey).WithMessage("A month looks like 2026-10.");
    }
}
