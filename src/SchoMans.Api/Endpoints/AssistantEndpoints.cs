using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.PrincipalAssistant;
using SchoMans.Application.Features.PrincipalAssistant.AskAssistant;

namespace SchoMans.Api.Endpoints;

public static class AssistantEndpoints
{
    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/assistant")
            .WithTags("Principal assistant")
            .RequireAuthorization(Policies.Principal)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/ask", Ask)
            .WithValidation<AskAssistantCommand>()
            .WithName("AskPrincipalAssistant");

        return app;
    }

    private static async Task<Results<Ok<AssistantAnswerDto>, ProblemHttpResult>> Ask(
        [FromBody] AskAssistantCommand command, AskAssistantHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();
}
