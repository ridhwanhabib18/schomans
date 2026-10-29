using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.GradeBands;
using SchoMans.Application.Features.GradeBands.ListGradeBands;
using SchoMans.Application.Features.GradeBands.ReplaceGradeBands;

namespace SchoMans.Api.Endpoints;

public static class GradeBandEndpoints
{
    public static IEndpointRouteBuilder MapGradeBandEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/grade-bands")
            .WithTags("Grade bands")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListGradeBands)
            .WithName("ListGradeBands");

        group.MapPut("/", ReplaceGradeBands)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<ReplaceGradeBandsCommand>()
            .WithName("ReplaceGradeBands");

        return app;
    }

    private static async Task<Results<Ok<List<GradeBandDto>>, ProblemHttpResult>> ListGradeBands(
        ListGradeBandsHandler handler, CancellationToken ct)
        => (await handler.Handle(new ListGradeBandsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<List<GradeBandDto>>, ProblemHttpResult>> ReplaceGradeBands(
        [FromBody] ReplaceGradeBandsCommand command, ReplaceGradeBandsHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();
}
