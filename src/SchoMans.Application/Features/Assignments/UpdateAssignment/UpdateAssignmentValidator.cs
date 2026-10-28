using FluentValidation;
using SchoMans.Application.Common;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Assignments.UpdateAssignment;

public sealed class UpdateAssignmentValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Assignment.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(Assignment.DescriptionMaxLength);
        RuleFor(x => x.ClassroomIds).NotEmpty().WithMessage("Choose at least one classroom.");
        RuleFor(x => (x.KeepAttachmentIds == null ? 0 : x.KeepAttachmentIds.Count) + (x.NewAttachments == null ? 0 : x.NewAttachments.Count))
            .LessThanOrEqualTo(Assignment.MaxAttachments)
            .OverridePropertyName("Attachments")
            .WithMessage($"An assignment has at most {Assignment.MaxAttachments} attachments.");
        RuleForEach(x => x.NewAttachments).ChildRules(file =>
        {
            file.RuleFor(f => f.Key).NotEmpty().MaximumLength(300);
            file.RuleFor(f => f.FileName).NotEmpty().MaximumLength(AssignmentAttachment.FileNameMaxLength);
        });
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
