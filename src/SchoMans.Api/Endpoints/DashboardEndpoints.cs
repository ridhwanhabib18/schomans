using Microsoft.AspNetCore.Http.HttpResults;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Dashboard.GetDashboard;

namespace SchoMans.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", GetDashboard)
            .WithName("GetDashboard");

        return app;
    }

    private static async Task<Results<Ok<DashboardDto>, ProblemHttpResult>> GetDashboard(
        GetDashboardHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetDashboardQuery(), ct)).ToHttpResult();
}
