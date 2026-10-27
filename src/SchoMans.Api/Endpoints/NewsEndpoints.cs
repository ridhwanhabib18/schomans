using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.News;
using SchoMans.Application.Features.News.CreateNews;
using SchoMans.Application.Features.News.DeleteNews;
using SchoMans.Application.Features.News.GetNews;
using SchoMans.Application.Features.News.GetPublicNews;
using SchoMans.Application.Features.News.ListNews;
using SchoMans.Application.Features.News.ListPublicNews;
using SchoMans.Application.Features.News.PublishNews;
using SchoMans.Application.Features.News.UnpublishNews;
using SchoMans.Application.Features.News.UpdateNews;

namespace SchoMans.Api.Endpoints;

public static class NewsEndpoints
{
    public static IEndpointRouteBuilder MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/news")
            .WithTags("News")
            .RequireAuthorization(Policies.Editors)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // The public side: anybody can read published articles. AllowAnonymous overrides the group's policy.
        group.MapGet("/public", ListPublicNews)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Public)
            .WithValidation<ListPublicNewsQuery>()
            .WithName("ListPublicNews");

        group.MapGet("/public/{slug}", GetPublicNews)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Public)
            .WithName("GetPublicNews");

        // The editors' side.
        group.MapGet("/", ListNews)
            .WithValidation<ListNewsQuery>()
            .WithName("ListNews");

        group.MapGet("/{id:guid}", GetNews)
            .WithName("GetNews");

        group.MapPost("/", CreateNews)
            .WithValidation<CreateNewsCommand>()
            .WithName("CreateNews");

        group.MapPut("/{id:guid}", UpdateNews)
            .WithValidation<UpdateNewsCommand>()
            .WithName("UpdateNews");

        group.MapPost("/{id:guid}/publish", PublishNews)
            .WithName("PublishNews");

        group.MapPost("/{id:guid}/unpublish", UnpublishNews)
            .WithName("UnpublishNews");

        group.MapDelete("/{id:guid}", DeleteNews)
            .WithName("DeleteNews");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<PublicNewsSummaryDto>>, ProblemHttpResult>> ListPublicNews(
        [AsParameters] ListPublicNewsQuery query, ListPublicNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<PublicNewsDto>, ProblemHttpResult>> GetPublicNews(
        [FromRoute] string slug, GetPublicNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetPublicNewsQuery(slug), ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<NewsDto>>, ProblemHttpResult>> ListNews(
        [AsParameters] ListNewsQuery query, ListNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<NewsDto>, ProblemHttpResult>> GetNews(
        [FromRoute] Guid id, GetNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetNewsQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<NewsDto>, ProblemHttpResult>> CreateNews(
        [FromBody] CreateNewsCommand command, CreateNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(article => $"/api/v1/news/{article.Id}");

    private static async Task<Results<Ok<NewsDto>, ProblemHttpResult>> UpdateNews(
        [FromRoute] Guid id, [FromBody] UpdateNewsCommand command, UpdateNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<Ok<NewsDto>, ProblemHttpResult>> PublishNews(
        [FromRoute] Guid id, PublishNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(new PublishNewsCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<NewsDto>, ProblemHttpResult>> UnpublishNews(
        [FromRoute] Guid id, UnpublishNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(new UnpublishNewsCommand(id), ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteNews(
        [FromRoute] Guid id, DeleteNewsHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteNewsCommand(id), ct)).ToNoContentResult();
}
