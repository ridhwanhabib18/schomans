using FluentValidation;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.Assignments.CreateAssignment;

public sealed class CreateAssignmentValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Assignment.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(Assignment.DescriptionMaxLength);
        RuleFor(x => x.DueAt).Must(due => due > timeProvider.GetUtcNow()).WithMessage("The due date must be in the future.");
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.ClassroomIds).NotEmpty().WithMessage("Choose at least one classroom.");
        RuleForEach(x => x.ClassroomIds).NotEmpty();
        RuleFor(x => x.Attachments!.Count).LessThanOrEqualTo(Assignment.MaxAttachments).When(x => x.Attachments is not null)
            .WithMessage($"An assignment has at most {Assignment.MaxAttachments} attachments.");
        RuleForEach(x => x.Attachments).ChildRules(file =>
        {
            file.RuleFor(f => f.Key).NotEmpty().MaximumLength(300);
            file.RuleFor(f => f.FileName).NotEmpty().MaximumLength(AssignmentAttachment.FileNameMaxLength);
        });
    }
}
