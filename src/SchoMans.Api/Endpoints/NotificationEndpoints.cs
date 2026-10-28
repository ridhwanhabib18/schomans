using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Notifications;
using SchoMans.Application.Features.Notifications.GetUnreadNotificationCount;
using SchoMans.Application.Features.Notifications.ListMyNotifications;
using SchoMans.Application.Features.Notifications.MarkAllNotificationsRead;
using SchoMans.Application.Features.Notifications.MarkNotificationRead;

namespace SchoMans.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListMyNotifications)
            .WithValidation<ListMyNotificationsQuery>()
            .WithName("ListMyNotifications");

        group.MapGet("/unread-count", GetUnreadNotificationCount)
            .WithName("GetUnreadNotificationCount");

        group.MapPost("/{id:guid}/read", MarkNotificationRead)
            .WithName("MarkNotificationRead");

        group.MapPost("/read-all", MarkAllNotificationsRead)
            .WithName("MarkAllNotificationsRead");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<NotificationDto>>, ProblemHttpResult>> ListMyNotifications(
        [AsParameters] ListMyNotificationsQuery query, ListMyNotificationsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<UnreadNotificationCountDto>, ProblemHttpResult>> GetUnreadNotificationCount(
        GetUnreadNotificationCountHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetUnreadNotificationCountQuery(), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> MarkNotificationRead(
        [FromRoute] Guid id, MarkNotificationReadHandler handler, CancellationToken ct)
        => (await handler.Handle(new MarkNotificationReadCommand(id), ct)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> MarkAllNotificationsRead(
        MarkAllNotificationsReadHandler handler, CancellationToken ct)
        => (await handler.Handle(new MarkAllNotificationsReadCommand(), ct)).ToNoContentResult();
}
