using FluentValidation;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Assignments.GradeSubmission;

public sealed class GradeSubmissionValidator : AbstractValidator<GradeSubmissionCommand>
{
    public GradeSubmissionValidator()
    {
        RuleFor(x => x.Score).InclusiveBetween(0m, 100m);
        RuleFor(x => x.Score).Must(score => decimal.Round(score, 2) == score).WithMessage("A score has at most two decimals.");
        RuleFor(x => x.Feedback).MaximumLength(Submission.FeedbackMaxLength);
    }
}
