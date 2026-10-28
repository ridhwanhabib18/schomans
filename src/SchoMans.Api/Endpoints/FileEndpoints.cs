using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Files.CreateUploadUrl;

namespace SchoMans.Api.Endpoints;

public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/files")
            .WithTags("Files")
            .RequireAuthorization(Policies.Editors)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/upload-url", CreateUploadUrl)
            .RequireRateLimiting(RateLimitPolicies.Uploads)
            .WithValidation<CreateUploadUrlCommand>()
            .WithName("CreateUploadUrl");

        return app;
    }

    private static async Task<Results<Ok<UploadUrlDto>, ProblemHttpResult>> CreateUploadUrl(
        [FromBody] CreateUploadUrlCommand command, CreateUploadUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();
}
