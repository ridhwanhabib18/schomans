using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Announcements;
using SchoMans.Application.Features.Announcements.CreateAnnouncement;
using SchoMans.Application.Features.Announcements.DeleteAnnouncement;
using SchoMans.Application.Features.Announcements.GetAnnouncement;
using SchoMans.Application.Features.Announcements.GetMyAnnouncement;
using SchoMans.Application.Features.Announcements.GetPublicAnnouncement;
using SchoMans.Application.Features.Announcements.ListAnnouncements;
using SchoMans.Application.Features.Announcements.ListMyAnnouncements;
using SchoMans.Application.Features.Announcements.ListPublicAnnouncements;
using SchoMans.Application.Features.Announcements.MarkAnnouncementRead;
using SchoMans.Application.Features.Announcements.PublishAnnouncement;
using SchoMans.Application.Features.Announcements.UpdateAnnouncement;

namespace SchoMans.Api.Endpoints;

public static class AnnouncementEndpoints
{
    public static IEndpointRouteBuilder MapAnnouncementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/announcements")
            .WithTags("Announcements")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Guests: announcements marked public.
        group.MapGet("/public", ListPublicAnnouncements)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Public)
            .WithValidation<ListPublicAnnouncementsQuery>()
            .WithName("ListPublicAnnouncements");

        group.MapGet("/public/{id:guid}", GetPublicAnnouncement)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Public)
            .WithName("GetPublicAnnouncement");

        // Every signed-in user: the inbox of announcements meant for them.
        group.MapGet("/mine", ListMyAnnouncements)
            .WithValidation<ListMyAnnouncementsQuery>()
            .WithName("ListMyAnnouncements");

        group.MapGet("/mine/{id:guid}", GetMyAnnouncement)
            .WithName("GetMyAnnouncement");

        group.MapPost("/mine/{id:guid}/read", MarkAnnouncementRead)
            .WithName("MarkAnnouncementRead");

        // Editors: admin staff and the principal.
        group.MapGet("/", ListAnnouncements)
            .RequireAuthorization(Policies.Editors)
            .WithValidation<ListAnnouncementsQuery>()
            .WithName("ListAnnouncements");

        group.MapGet("/{id:guid}", GetAnnouncement)
            .RequireAuthorization(Policies.Editors)
            .WithName("GetAnnouncement");

        group.MapPost("/", CreateAnnouncement)
            .RequireAuthorization(Policies.Editors)
            .WithValidation<CreateAnnouncementCommand>()
            .WithName("CreateAnnouncement");

        group.MapPut("/{id:guid}", UpdateAnnouncement)
            .RequireAuthorization(Policies.Editors)
            .WithValidation<UpdateAnnouncementCommand>()
            .WithName("UpdateAnnouncement");

        group.MapPost("/{id:guid}/publish", PublishAnnouncement)
            .RequireAuthorization(Policies.Editors)
            .WithName("PublishAnnouncement");

        group.MapDelete("/{id:guid}", DeleteAnnouncement)
            .RequireAuthorization(Policies.Editors)
            .WithName("DeleteAnnouncement");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<AnnouncementSummaryDto>>, ProblemHttpResult>> ListPublicAnnouncements(
        [AsParameters] ListPublicAnnouncementsQuery query, ListPublicAnnouncementsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AnnouncementReaderDto>, ProblemHttpResult>> GetPublicAnnouncement(
        [FromRoute] Guid id, GetPublicAnnouncementHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetPublicAnnouncementQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<AnnouncementSummaryDto>>, ProblemHttpResult>> ListMyAnnouncements(
        [AsParameters] ListMyAnnouncementsQuery query, ListMyAnnouncementsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AnnouncementReaderDto>, ProblemHttpResult>> GetMyAnnouncement(
        [FromRoute] Guid id, GetMyAnnouncementHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetMyAnnouncementQuery(id), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> MarkAnnouncementRead(
        [FromRoute] Guid id, MarkAnnouncementReadHandler handler, CancellationToken ct)
        => (await handler.Handle(new MarkAnnouncementReadCommand(id), ct)).ToNoContentResult();

    private static async Task<Results<Ok<PagedResult<AnnouncementDto>>, ProblemHttpResult>> ListAnnouncements(
        [AsParameters] ListAnnouncementsQuery query, ListAnnouncementsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AnnouncementDto>, ProblemHttpResult>> GetAnnouncement(
        [FromRoute] Guid id, GetAnnouncementHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetAnnouncementQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<AnnouncementDto>, ProblemHttpResult>> CreateAnnouncement(
        [FromBody] CreateAnnouncementCommand command, CreateAnnouncementHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(announcement => $"/api/v1/announcements/{announcement.Id}");

    private static async Task<Results<Ok<AnnouncementDto>, ProblemHttpResult>> UpdateAnnouncement(
        [FromRoute] Guid id, [FromBody] UpdateAnnouncementCommand command, UpdateAnnouncementHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<Ok<PublishAnnouncementSummary>, ProblemHttpResult>> PublishAnnouncement(
        [FromRoute] Guid id, PublishAnnouncementHandler handler, CancellationToken ct)
        => (await handler.Handle(new PublishAnnouncementCommand(id), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAnnouncement(
        [FromRoute] Guid id, DeleteAnnouncementHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteAnnouncementCommand(id), ct)).ToNoContentResult();
}
