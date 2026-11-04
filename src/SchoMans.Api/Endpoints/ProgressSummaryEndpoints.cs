using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.ProgressSummaries;
using SchoMans.Application.Features.ProgressSummaries.ApproveProgressSummary;
using SchoMans.Application.Features.ProgressSummaries.EditProgressSummary;
using SchoMans.Application.Features.ProgressSummaries.ListClassroomProgressSummaries;
using SchoMans.Application.Features.ProgressSummaries.ListMyProgressSummaries;
using SchoMans.Application.Features.ProgressSummaries.RegenerateProgressSummary;
using SchoMans.Application.Features.ProgressSummaries.SendProgressSummaries;

namespace SchoMans.Api.Endpoints;

public static class ProgressSummaryEndpoints
{
    public static IEndpointRouteBuilder MapProgressSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/progress-summaries")
            .WithTags("Progress summaries")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Homeroom teachers: review the drafts of their classroom (the handler checks the classroom is theirs).
        group.MapGet("/", ListClassroomProgressSummaries)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<ListClassroomProgressSummariesQuery>()
            .WithName("ListClassroomProgressSummaries");

        group.MapPost("/regenerate", RegenerateProgressSummary)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<RegenerateProgressSummaryCommand>()
            .WithName("RegenerateProgressSummary");

        group.MapPut("/{id:guid}", EditProgressSummary)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<EditProgressSummaryCommand>()
            .WithName("EditProgressSummary");

        group.MapPost("/{id:guid}/approve", ApproveProgressSummary)
            .RequireAuthorization(Policies.Teacher)
            .WithName("ApproveProgressSummary");

        group.MapPost("/send", SendProgressSummaries)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<SendProgressSummariesCommand>()
            .WithName("SendProgressSummaries");

        // Parents: the history of the summaries that were sent about their children.
        group.MapGet("/mine", ListMyProgressSummaries)
            .RequireAuthorization(Policies.Parent)
            .WithValidation<ListMyProgressSummariesQuery>()
            .WithName("ListMyProgressSummaries");

        return app;
    }

    private static async Task<Results<Ok<List<ProgressSummaryDto>>, ProblemHttpResult>> ListClassroomProgressSummaries(
        [AsParameters] ListClassroomProgressSummariesQuery query, ListClassroomProgressSummariesHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<ProgressSummaryDto>, ProblemHttpResult>> RegenerateProgressSummary(
        [FromBody] RegenerateProgressSummaryCommand command, RegenerateProgressSummaryHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<ProgressSummaryDto>, ProblemHttpResult>> EditProgressSummary(
        [FromRoute] Guid id, [FromBody] EditProgressSummaryCommand command, EditProgressSummaryHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<Ok<ProgressSummaryDto>, ProblemHttpResult>> ApproveProgressSummary(
        [FromRoute] Guid id, ApproveProgressSummaryHandler handler, CancellationToken ct)
        => (await handler.Handle(new ApproveProgressSummaryCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<SentProgressSummariesDto>, ProblemHttpResult>> SendProgressSummaries(
        [FromBody] SendProgressSummariesCommand command, SendProgressSummariesHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<ProgressSummaryDto>>, ProblemHttpResult>> ListMyProgressSummaries(
        [AsParameters] ListMyProgressSummariesQuery query, ListMyProgressSummariesHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();
}
