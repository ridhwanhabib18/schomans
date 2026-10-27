using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Announcements.ListMyAnnouncements;

public sealed class ListMyAnnouncementsValidator : AbstractValidator<ListMyAnnouncementsQuery>
{
    public ListMyAnnouncementsValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}
