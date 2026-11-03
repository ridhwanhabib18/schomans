using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.LearningObjectives;
using SchoMans.Application.Features.LearningObjectives.CreateLearningObjective;
using SchoMans.Application.Features.LearningObjectives.DeleteLearningObjective;
using SchoMans.Application.Features.LearningObjectives.ListLearningObjectives;
using SchoMans.Application.Features.LearningObjectives.UpdateLearningObjective;

namespace SchoMans.Api.Endpoints;

public static class LearningObjectiveEndpoints
{
    public static IEndpointRouteBuilder MapLearningObjectiveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/learning-objectives")
            .WithTags("Learning objectives")
            .RequireAuthorization(Policies.Teacher)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListLearningObjectives)
            .WithName("ListLearningObjectives");

        group.MapPost("/", CreateLearningObjective)
            .WithValidation<CreateLearningObjectiveCommand>()
            .WithName("CreateLearningObjective");

        group.MapPut("/{id:guid}", UpdateLearningObjective)
            .WithValidation<UpdateLearningObjectiveCommand>()
            .WithName("UpdateLearningObjective");

        group.MapDelete("/{id:guid}", DeleteLearningObjective)
            .WithName("DeleteLearningObjective");

        return app;
    }

    private static async Task<Results<Ok<List<LearningObjectiveDto>>, ProblemHttpResult>> ListLearningObjectives(
        [AsParameters] ListLearningObjectivesQuery query, ListLearningObjectivesHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<LearningObjectiveDto>, ProblemHttpResult>> CreateLearningObjective(
        [FromBody] CreateLearningObjectiveCommand command, CreateLearningObjectiveHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<LearningObjectiveDto>, ProblemHttpResult>> UpdateLearningObjective(
        [FromRoute] Guid id, [FromBody] UpdateLearningObjectiveCommand command, UpdateLearningObjectiveHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteLearningObjective(
        [FromRoute] Guid id, DeleteLearningObjectiveHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteLearningObjectiveCommand(id), ct)).ToNoContentResult();
}
