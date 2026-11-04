using FluentValidation;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.ProgressSummaries.EditProgressSummary;

public sealed class EditProgressSummaryValidator : AbstractValidator<EditProgressSummaryCommand>
{
    public EditProgressSummaryValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(ProgressSummary.BodyMaxLength);
    }
}
