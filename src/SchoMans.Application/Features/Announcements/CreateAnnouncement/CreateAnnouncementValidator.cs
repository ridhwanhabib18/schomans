using FluentValidation;

namespace SchoMans.Application.Features.Announcements.CreateAnnouncement;

public sealed class CreateAnnouncementValidator : AbstractValidator<CreateAnnouncementCommand>
{
    public CreateAnnouncementValidator()
    {
        RuleFor(x => x.Title).MustBeTitle();
        RuleFor(x => x.Body).MustBeBody();
        RuleFor(x => x.TargetRoles).MustBeTargetRoles();
        RuleFor(x => x.TargetClassroomIds).MustBeTargetClassrooms();
    }
}
