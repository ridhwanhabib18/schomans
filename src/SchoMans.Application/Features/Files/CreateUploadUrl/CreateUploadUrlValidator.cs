using FluentValidation;

namespace SchoMans.Application.Features.Files.CreateUploadUrl;

public sealed class CreateUploadUrlValidator : AbstractValidator<CreateUploadUrlCommand>
{
    public CreateUploadUrlValidator()
    {
        RuleFor(x => x.Module)
            .Must(m => m is CoverImages.NewsModule or CoverImages.AnnouncementsModule)
            .WithMessage("The module must be 'news' or 'announcements'.");
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.ContentType)
            .Must(type => CoverImageRules.ExtensionByContentType.ContainsKey(type ?? string.Empty))
            .WithMessage("Only JPEG, PNG and WebP images are accepted.");
        RuleFor(x => x.SizeBytes)
            .InclusiveBetween(1, CoverImageRules.MaxBytes)
            .WithMessage("The image must be larger than 0 and at most 5 MB.");
    }
}
