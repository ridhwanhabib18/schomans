using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Announcements.UpdateAnnouncement;

public sealed class UpdateAnnouncementValidator : AbstractValidator<UpdateAnnouncementCommand>
{
    public UpdateAnnouncementValidator()
    {
        RuleFor(x => x.Title).MustBeTitle();
        RuleFor(x => x.Body).MustBeBody();
        RuleFor(x => x.TargetRoles).MustBeTargetRoles();
        RuleFor(x => x.TargetClassroomIds).MustBeTargetClassrooms();
        RuleFor(x => x.CoverImageKey).MaximumLength(300);
        RuleFor(x => x.RowVersion).MustBeRowVersion();
    }
}
