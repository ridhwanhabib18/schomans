using Microsoft.AspNetCore.Http.HttpResults;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Students.ListMyChildren;
using SchoMans.Application.Features.Users.GetMe;

namespace SchoMans.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser");

        group.MapGet("/me/children", ListMyChildren)
            .RequireAuthorization(Policies.Parent)
            .WithName("ListMyChildren");

        return app;
    }

    private static async Task<Results<Ok<MeDto>, ProblemHttpResult>> GetCurrentUser(
        GetMeHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetMeQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ChildDto>>, ProblemHttpResult>> ListMyChildren(
        ListMyChildrenHandler handler, CancellationToken ct)
        => (await handler.Handle(ct)).ToHttpResult();
}
