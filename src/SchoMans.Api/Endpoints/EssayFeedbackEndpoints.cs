using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.EssayFeedback;

namespace SchoMans.Api.Endpoints;

public static class EssayFeedbackEndpoints
{
    public static IEndpointRouteBuilder MapEssayFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/essay-feedback")
            .WithTags("Essay feedback")
            .RequireAuthorization(Policies.Teacher)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/assignments/{assignmentId:guid}", ListFeedbackDrafts)
            .WithName("ListFeedbackDrafts");

        group.MapPost("/assignments/{assignmentId:guid}/draft", DraftAllFeedback)
            .WithName("DraftAllFeedback");

        group.MapGet("/submissions/{submissionId:guid}", GetFeedbackDraft)
            .WithName("GetFeedbackDraft");

        group.MapPost("/submissions/{submissionId:guid}/draft", DraftFeedback)
            .WithName("DraftFeedback");

        group.MapPost("/submissions/{submissionId:guid}/accept", AcceptFeedback)
            .WithValidation<AcceptFeedbackCommand>()
            .WithName("AcceptFeedback");

        group.MapPost("/submissions/{submissionId:guid}/reject", RejectFeedback)
            .WithName("RejectFeedback");

        return app;
    }

    private static async Task<Results<Ok<List<FeedbackDraftSummaryDto>>, ProblemHttpResult>> ListFeedbackDrafts(
        [FromRoute] Guid assignmentId, ListFeedbackDraftsHandler handler, CancellationToken ct)
        => (await handler.Handle(new ListFeedbackDraftsQuery(assignmentId), ct)).ToHttpResult();

    private static async Task<Results<Ok<DraftedFeedbackDto>, ProblemHttpResult>> DraftAllFeedback(
        [FromRoute] Guid assignmentId, DraftAllFeedbackHandler handler, CancellationToken ct)
        => (await handler.Handle(new DraftAllFeedbackCommand(assignmentId), ct)).ToHttpResult();

    private static async Task<Results<Ok<FeedbackDraftDto>, ProblemHttpResult>> GetFeedbackDraft(
        [FromRoute] Guid submissionId, GetFeedbackDraftHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetFeedbackDraftQuery(submissionId), ct)).ToHttpResult();

    private static async Task<Results<Ok<FeedbackDraftDto>, ProblemHttpResult>> DraftFeedback(
        [FromRoute] Guid submissionId, DraftFeedbackHandler handler, CancellationToken ct)
        => (await handler.Handle(new DraftFeedbackCommand(submissionId), ct)).ToHttpResult();

    private static async Task<Results<Ok<FeedbackDraftDto>, ProblemHttpResult>> AcceptFeedback(
        [FromRoute] Guid submissionId, [FromBody] AcceptFeedbackCommand command, AcceptFeedbackHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { SubmissionId = submissionId }, ct)).ToHttpResult();

    private static async Task<Results<Ok<FeedbackDraftDto>, ProblemHttpResult>> RejectFeedback(
        [FromRoute] Guid submissionId, RejectFeedbackHandler handler, CancellationToken ct)
        => (await handler.Handle(new RejectFeedbackCommand(submissionId), ct)).ToHttpResult();
}
