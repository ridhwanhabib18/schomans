using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.GeneralLetters;
using SchoMans.Application.Features.Letters;

namespace SchoMans.Api.Endpoints;

public static class GeneralLetterEndpoints
{
    public static IEndpointRouteBuilder MapGeneralLetterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/general-letters")
            .WithTags("General letters")
            .RequireAuthorization(Policies.Editors)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListGeneralLetters)
            .WithValidation<ListGeneralLettersQuery>()
            .WithName("ListGeneralLetters");

        group.MapPost("/", IssueGeneralLetter)
            .WithValidation<IssueGeneralLetterCommand>()
            .WithName("IssueGeneralLetter");

        group.MapGet("/{id:guid}/url", GetGeneralLetterUrl)
            .WithName("GetGeneralLetterUrl");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<GeneralLetterDto>>, ProblemHttpResult>> ListGeneralLetters(
        [AsParameters] ListGeneralLettersQuery query, ListGeneralLettersHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Created<GeneralLetterDto>, ProblemHttpResult>> IssueGeneralLetter(
        [FromBody] IssueGeneralLetterCommand command, IssueGeneralLetterHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(letter => $"/api/v1/general-letters/{letter.Id}/url");

    private static async Task<Results<Ok<LetterUrlDto>, ProblemHttpResult>> GetGeneralLetterUrl(
        [FromRoute] Guid id, GetGeneralLetterUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetGeneralLetterUrlQuery(id), ct)).ToHttpResult();
}
