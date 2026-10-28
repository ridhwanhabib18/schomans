using FluentValidation;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Assignments.SubmitAssignment;

public sealed class SubmitAssignmentValidator : AbstractValidator<SubmitAssignmentCommand>
{
    public SubmitAssignmentValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.FileKey) || !string.IsNullOrWhiteSpace(x.TextBody) || x.KeepFile)
            .OverridePropertyName("TextBody")
            .WithMessage("Hand in a file or write your answer.");
        RuleFor(x => x.FileKey).MaximumLength(300);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(AssignmentAttachment.FileNameMaxLength).When(x => !string.IsNullOrWhiteSpace(x.FileKey));
        RuleFor(x => x.TextBody).MaximumLength(Submission.TextMaxLength);
    }
}
