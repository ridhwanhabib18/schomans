using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Letters;

namespace SchoMans.Api.Endpoints;

public static class LetterEndpoints
{
    public static IEndpointRouteBuilder MapLetterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/letters")
            .WithTags("Letters")
            .RequireAuthorization(Policies.RequestUsers)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // The authenticity check behind the QR code: anybody with the letter, no account. AllowAnonymous overrides the group's policy.
        group.MapGet("/verify/{code}", VerifyLetter)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Public)
            .WithName("VerifyLetter");

        group.MapGet("/{requestId:guid}/url", GetLetterUrl)
            .WithName("GetLetterUrl");

        return app;
    }

    private static async Task<Results<Ok<LetterVerificationDto>, ProblemHttpResult>> VerifyLetter(
        [FromRoute] string code, VerifyLetterHandler handler, CancellationToken ct)
        => (await handler.Handle(new VerifyLetterQuery(code), ct)).ToHttpResult();

    private static async Task<Results<Ok<LetterUrlDto>, ProblemHttpResult>> GetLetterUrl(
        [FromRoute] Guid requestId, GetLetterUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetLetterUrlQuery(requestId), ct)).ToHttpResult();
}
