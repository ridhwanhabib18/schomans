using FluentValidation;
using SchoMans.Application.Common;

namespace SchoMans.Application.Features.Notifications.ListMyNotifications;

public sealed class ListMyNotificationsValidator : AbstractValidator<ListMyNotificationsQuery>
{
    public ListMyNotificationsValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}
