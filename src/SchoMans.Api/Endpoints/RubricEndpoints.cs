using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Rubrics;

namespace SchoMans.Api.Endpoints;

public static class RubricEndpoints
{
    public static IEndpointRouteBuilder MapRubricEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/rubrics")
            .WithTags("Rubrics")
            .RequireAuthorization(Policies.Teacher)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{assignmentId:guid}", GetRubric)
            .WithName("GetRubric");

        group.MapPut("/{assignmentId:guid}", SetRubric)
            .WithValidation<SetRubricCommand>()
            .WithName("SetRubric");

        return app;
    }

    private static async Task<Results<Ok<RubricDto>, ProblemHttpResult>> GetRubric(
        [FromRoute] Guid assignmentId, GetRubricHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetRubricQuery(assignmentId), ct)).ToHttpResult();

    private static async Task<Results<Ok<RubricDto>, ProblemHttpResult>> SetRubric(
        [FromRoute] Guid assignmentId, [FromBody] SetRubricCommand command, SetRubricHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { AssignmentId = assignmentId }, ct)).ToHttpResult();
}
