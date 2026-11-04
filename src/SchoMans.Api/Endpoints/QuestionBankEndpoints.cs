using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.QuestionBank;
using SchoMans.Domain.Assignments;

namespace SchoMans.Api.Endpoints;

public static class QuestionBankEndpoints
{
    private const long GenerateRequestLimit = 11 * 1024 * 1024;

    public static IEndpointRouteBuilder MapQuestionBankEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/question-bank")
            .WithTags("Question bank")
            .RequireAuthorization(Policies.Teacher)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListQuestions)
            .WithValidation<ListQuestionsQuery>()
            .WithName("ListQuestions");

        group.MapPost("/", SaveQuestions)
            .WithValidation<SaveQuestionsCommand>()
            .WithName("SaveQuestions");

        group.MapPut("/{id:guid}", UpdateQuestion)
            .WithValidation<UpdateQuestionCommand>()
            .WithName("UpdateQuestion");

        group.MapDelete("/{id:guid}", DeleteQuestion)
            .WithName("DeleteQuestion");

        group.MapPost("/generate", GenerateQuestions)
            .RequireRateLimiting(RateLimitPolicies.Uploads)
            .WithMetadata(new RequestSizeLimitAttribute(GenerateRequestLimit))
            .DisableAntiforgery() // the API uses JWT bearer tokens, not cookies
            .WithName("GenerateQuestions");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<QuestionDto>>, ProblemHttpResult>> ListQuestions(
        [AsParameters] ListQuestionsQuery query, ListQuestionsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Created<List<QuestionDto>>, ProblemHttpResult>> SaveQuestions(
        [FromBody] SaveQuestionsCommand command, SaveQuestionsHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(_ => "/api/v1/question-bank");

    private static async Task<Results<Ok<QuestionDto>, ProblemHttpResult>> UpdateQuestion(
        [FromRoute] Guid id, [FromBody] UpdateQuestionCommand command, UpdateQuestionHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteQuestion(
        [FromRoute] Guid id, DeleteQuestionHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteQuestionCommand(id), ct)).ToNoContentResult();

    private static async Task<Results<Ok<GeneratedQuestionsDto>, ProblemHttpResult>> GenerateQuestions(
        IFormFile file,
        [FromForm] Guid subjectId,
        [FromForm] int count,
        [FromForm] QuestionDifficulty difficulty,
        [FromForm] QuestionType type,
        GenerateQuestionsHandler handler,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return (await handler.Handle(new GenerateQuestionsCommand(subjectId, count, difficulty, type, stream, file.FileName, file.Length), ct)).ToHttpResult();
    }
}
