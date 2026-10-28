using FluentValidation;

namespace SchoMans.Application.Features.Assignments.CreateAttachmentUploadUrl;

public sealed class CreateAttachmentUploadUrlValidator : AbstractValidator<CreateAttachmentUploadUrlCommand>
{
    public CreateAttachmentUploadUrlValidator()
    {
        RuleFor(x => x.ContentType)
            .Must(type => AssignmentFileRules.AttachmentTypes.ContainsKey(type ?? string.Empty))
            .WithMessage("Attach a PDF, an image (JPEG, PNG, WebP) or a Word, Excel or PowerPoint file.");
        RuleFor(x => x.SizeBytes)
            .InclusiveBetween(1, AssignmentFileRules.MaxBytes)
            .WithMessage("The file must be larger than 0 and at most 10 MB.");
    }
}
