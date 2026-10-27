using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Announcements.ListAnnouncements;

public sealed class ListAnnouncementsValidator : AbstractValidator<ListAnnouncementsQuery>
{
    public ListAnnouncementsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
