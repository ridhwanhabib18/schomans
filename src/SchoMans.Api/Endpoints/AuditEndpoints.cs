using Microsoft.AspNetCore.Http.HttpResults;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Audit.ListAuditLogs;

namespace SchoMans.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit-logs")
            .WithTags("Audit log")
            .RequireAuthorization(Policies.Editors)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListAuditLogs)
            .WithValidation<ListAuditLogsQuery>()
            .WithName("ListAuditLogs");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<AuditLogDto>>, ProblemHttpResult>> ListAuditLogs(
        [AsParameters] ListAuditLogsQuery query, ListAuditLogsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();
}
