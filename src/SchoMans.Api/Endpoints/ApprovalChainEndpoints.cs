using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Requests;
using SchoMans.Application.Features.Requests.Chains;

namespace SchoMans.Api.Endpoints;

public static class ApprovalChainEndpoints
{
    public static IEndpointRouteBuilder MapApprovalChainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/approval-chains")
            .WithTags("Approval chains")
            .RequireAuthorization(Policies.Editors)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", GetApprovalChains)
            .WithName("GetApprovalChains");

        group.MapPut("/", SetApprovalChain)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<SetApprovalChainCommand>()
            .WithName("SetApprovalChain");

        return app;
    }

    private static async Task<Results<Ok<List<ApprovalChainDto>>, ProblemHttpResult>> GetApprovalChains(
        GetApprovalChainsHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetApprovalChainsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<List<ApprovalChainDto>>, ProblemHttpResult>> SetApprovalChain(
        [FromBody] SetApprovalChainCommand command, SetApprovalChainHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();
}
