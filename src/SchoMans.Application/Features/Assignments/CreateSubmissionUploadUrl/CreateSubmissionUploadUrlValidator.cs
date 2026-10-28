using FluentValidation;

namespace SchoMans.Application.Features.Assignments.CreateSubmissionUploadUrl;

public sealed class CreateSubmissionUploadUrlValidator : AbstractValidator<CreateSubmissionUploadUrlCommand>
{
    public CreateSubmissionUploadUrlValidator()
    {
        RuleFor(x => x.ContentType)
            .Must(type => AssignmentFileRules.SubmissionTypes.ContainsKey(type ?? string.Empty))
            .WithMessage("Hand in a PDF or an image (JPEG, PNG, WebP).");
        RuleFor(x => x.SizeBytes)
            .InclusiveBetween(1, AssignmentFileRules.MaxBytes)
            .WithMessage("The file must be larger than 0 and at most 10 MB.");
    }
}
