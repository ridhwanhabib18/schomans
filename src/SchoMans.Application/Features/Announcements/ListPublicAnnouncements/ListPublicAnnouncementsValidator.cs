using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Announcements.ListPublicAnnouncements;

public sealed class ListPublicAnnouncementsValidator : AbstractValidator<ListPublicAnnouncementsQuery>
{
    public ListPublicAnnouncementsValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}
