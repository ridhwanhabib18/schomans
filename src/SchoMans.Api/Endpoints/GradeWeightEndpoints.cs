using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.GradeWeights;
using SchoMans.Application.Features.GradeWeights.GetGradeWeights;
using SchoMans.Application.Features.GradeWeights.SetGradeWeights;

namespace SchoMans.Api.Endpoints;

public static class GradeWeightEndpoints
{
    public static IEndpointRouteBuilder MapGradeWeightEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/grade-weights")
            .WithTags("Grade weights")
            .RequireAuthorization(Policies.StaffRead)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{subjectId:guid}", GetGradeWeights)
            .WithName("GetGradeWeights");

        group.MapPut("/{subjectId:guid}", SetGradeWeights)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<SetGradeWeightsCommand>()
            .WithName("SetGradeWeights");

        return app;
    }

    private static async Task<Results<Ok<GradeWeightsDto>, ProblemHttpResult>> GetGradeWeights(
        [FromRoute] Guid subjectId, GetGradeWeightsHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetGradeWeightsQuery(subjectId), ct)).ToHttpResult();

    private static async Task<Results<Ok<GradeWeightsDto>, ProblemHttpResult>> SetGradeWeights(
        [FromRoute] Guid subjectId, [FromBody] SetGradeWeightsCommand command, SetGradeWeightsHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { SubjectId = subjectId }, ct)).ToHttpResult();
}
