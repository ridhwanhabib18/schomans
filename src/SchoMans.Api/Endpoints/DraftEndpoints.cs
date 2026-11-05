using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Drafts;

namespace SchoMans.Api.Endpoints;

public static class DraftEndpoints
{
    public static IEndpointRouteBuilder MapDraftEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/drafts")
            .WithTags("Document drafts")
            .RequireAuthorization(Policies.Editors)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", DraftDocument)
            .WithValidation<DraftDocumentCommand>()
            .WithName("DraftDocument");

        return app;
    }

    private static async Task<Results<Ok<DocumentDraftDto>, ProblemHttpResult>> DraftDocument(
        [FromBody] DraftDocumentCommand command, DraftDocumentHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();
}
