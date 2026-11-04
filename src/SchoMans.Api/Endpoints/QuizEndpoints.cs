using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.Quizzes;

namespace SchoMans.Api.Endpoints;

public static class QuizEndpoints
{
    public static IEndpointRouteBuilder MapQuizEndpoints(this IEndpointRouteBuilder app)
    {
        // A quiz is an assignment of type Quiz: the handlers check that it is the teacher's own, or given to the student's classroom, or to the parent's child.
        var group = app.MapGroup("/api/v1/quizzes")
            .WithTags("Quizzes")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", CreateQuiz)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<CreateQuizCommand>()
            .WithName("CreateQuiz");

        group.MapGet("/{assignmentId:guid}", GetQuiz)
            .WithName("GetQuiz");

        group.MapPost("/{assignmentId:guid}/submit", SubmitQuiz)
            .RequireAuthorization(Policies.Student)
            .WithValidation<SubmitQuizCommand>()
            .WithName("SubmitQuiz");

        group.MapPost("/{assignmentId:guid}/grade", GradeQuiz)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<GradeQuizCommand>()
            .WithName("GradeQuiz");

        return app;
    }

    private static async Task<Results<Created<AssignmentDto>, ProblemHttpResult>> CreateQuiz(
        [FromBody] CreateQuizCommand command, CreateQuizHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(quiz => $"/api/v1/quizzes/{quiz.Id}");

    private static async Task<Results<Ok<QuizDto>, ProblemHttpResult>> GetQuiz(
        [FromRoute] Guid assignmentId, [FromQuery] Guid? studentId, GetQuizHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetQuizQuery(assignmentId, studentId), ct)).ToHttpResult();

    private static async Task<Results<Ok<QuizDto>, ProblemHttpResult>> SubmitQuiz(
        [FromRoute] Guid assignmentId, [FromBody] SubmitQuizCommand command, SubmitQuizHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { AssignmentId = assignmentId }, ct)).ToHttpResult();

    private static async Task<Results<Ok<QuizDto>, ProblemHttpResult>> GradeQuiz(
        [FromRoute] Guid assignmentId, [FromBody] GradeQuizCommand command, GradeQuizHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { AssignmentId = assignmentId }, ct)).ToHttpResult();
}
