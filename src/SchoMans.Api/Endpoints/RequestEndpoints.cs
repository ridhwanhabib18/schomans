using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Requests;
using SchoMans.Application.Features.Requests.CancelRequest;
using SchoMans.Application.Features.Requests.CreateRequest;
using SchoMans.Application.Features.Requests.DecideRequest;
using SchoMans.Application.Features.Requests.GetRequest;
using SchoMans.Application.Features.Requests.ListRequests;

namespace SchoMans.Api.Endpoints;

public static class RequestEndpoints
{
    public static IEndpointRouteBuilder MapRequestEndpoints(this IEndpointRouteBuilder app)
    {
        // Who may do what is decided in Application (the role has to fit the kind of request and the step), the policy
        // only keeps out the roles that have nothing to do with requests.
        var group = app.MapGroup("/api/v1/requests")
            .WithTags("Requests")
            .RequireAuthorization(Policies.RequestUsers)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/mine", ListMyRequests)
            .WithValidation<ListMyRequestsQuery>()
            .WithName("ListMyRequests");

        group.MapGet("/inbox", ListInbox)
            .RequireAuthorization(Policies.Approvers)
            .WithValidation<ListInboxQuery>()
            .WithName("ListRequestInbox");

        group.MapGet("/", ListAllRequests)
            .RequireAuthorization(Policies.Editors)
            .WithValidation<ListAllRequestsQuery>()
            .WithName("ListAllRequests");

        group.MapGet("/{id:guid}", GetRequest)
            .WithName("GetRequest");

        group.MapPost("/", CreateRequest)
            .WithValidation<CreateRequestCommand>()
            .WithName("CreateRequest");

        group.MapPost("/{id:guid}/decision", DecideRequest)
            .RequireAuthorization(Policies.Approvers)
            .WithValidation<DecideRequestCommand>()
            .WithName("DecideRequest");

        group.MapPost("/{id:guid}/cancel", CancelRequest)
            .WithName("CancelRequest");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<RequestListItemDto>>, ProblemHttpResult>> ListMyRequests(
        [AsParameters] ListMyRequestsQuery query, ListMyRequestsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<RequestListItemDto>>, ProblemHttpResult>> ListInbox(
        [AsParameters] ListInboxQuery query, ListInboxHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<RequestListItemDto>>, ProblemHttpResult>> ListAllRequests(
        [AsParameters] ListAllRequestsQuery query, ListAllRequestsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<RequestDetailDto>, ProblemHttpResult>> GetRequest(
        [FromRoute] Guid id, GetRequestHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetRequestQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<RequestDetailDto>, ProblemHttpResult>> CreateRequest(
        [FromBody] CreateRequestCommand command, CreateRequestHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(request => $"/api/v1/requests/{request.Id}");

    private static async Task<Results<Ok<RequestDetailDto>, ProblemHttpResult>> DecideRequest(
        [FromRoute] Guid id, [FromBody] DecideRequestCommand command, DecideRequestHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<Ok<RequestDetailDto>, ProblemHttpResult>> CancelRequest(
        [FromRoute] Guid id, CancelRequestHandler handler, CancellationToken ct)
        => (await handler.Handle(new CancelRequestCommand(id), ct)).ToHttpResult();
}
