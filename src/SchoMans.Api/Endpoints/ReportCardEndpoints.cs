using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.AchievementDescriptions;
using SchoMans.Application.Features.AchievementDescriptions.AcceptAchievementDescription;
using SchoMans.Application.Features.AchievementDescriptions.DraftAchievementDescription;
using SchoMans.Application.Features.AchievementDescriptions.DraftAchievementDescriptions;
using SchoMans.Application.Features.ReportCards;
using SchoMans.Application.Features.ReportCards.GenerateReportCards;
using SchoMans.Application.Features.ReportCards.GetReportCardUrl;
using SchoMans.Application.Features.ReportCards.GetStudentGrades;
using SchoMans.Application.Features.ReportCards.ListStudentTerms;
using SchoMans.Application.Features.ReportCards.WriteAchievementDescription;

namespace SchoMans.Api.Endpoints;

public static class ReportCardEndpoints
{
    public static IEndpointRouteBuilder MapReportCardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/report-cards")
            .WithTags("Report cards")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Students and parents: the published grades of a term (the handler allows only the own student or the own children).
        group.MapGet("/grades", GetStudentGrades)
            .WithName("GetStudentGrades");

        group.MapGet("/terms", ListStudentTerms)
            .WithName("ListStudentTerms");

        group.MapGet("/url", GetReportCardUrl)
            .WithName("GetReportCardUrl");

        group.MapPut("/descriptions", WriteAchievementDescription)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<WriteAchievementDescriptionCommand>()
            .WithName("WriteAchievementDescription");

        // AI-02: the AI drafts; the teacher accepts, edits (the PUT above) or asks for a new draft.
        group.MapPost("/descriptions/draft", DraftAchievementDescription)
            .RequireAuthorization(Policies.Teacher)
            .WithName("DraftAchievementDescription");

        group.MapPost("/descriptions/draft-classroom", DraftAchievementDescriptions)
            .RequireAuthorization(Policies.Teacher)
            .WithName("DraftAchievementDescriptions");

        group.MapPost("/descriptions/accept", AcceptAchievementDescription)
            .RequireAuthorization(Policies.Teacher)
            .WithName("AcceptAchievementDescription");

        group.MapPost("/generate", GenerateReportCards)
            .RequireAuthorization(Policies.Teacher)
            .WithName("GenerateReportCards");

        return app;
    }

    private static async Task<Results<Ok<StudentGradesDto>, ProblemHttpResult>> GetStudentGrades(
        [AsParameters] GetStudentGradesQuery query, GetStudentGradesHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<List<StudentTermDto>>, ProblemHttpResult>> ListStudentTerms(
        [AsParameters] ListStudentTermsQuery query, ListStudentTermsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<ReportCardUrlDto>, ProblemHttpResult>> GetReportCardUrl(
        [AsParameters] GetReportCardUrlQuery query, GetReportCardUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AchievementDescriptionDto>, ProblemHttpResult>> WriteAchievementDescription(
        [FromBody] WriteAchievementDescriptionCommand command, WriteAchievementDescriptionHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<GeneratedReportCardsDto>, ProblemHttpResult>> GenerateReportCards(
        [FromBody] GenerateReportCardsCommand command, GenerateReportCardsHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<AchievementDescriptionDto>, ProblemHttpResult>> DraftAchievementDescription(
        [FromBody] DraftAchievementDescriptionCommand command, DraftAchievementDescriptionHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<DraftedDescriptionsDto>, ProblemHttpResult>> DraftAchievementDescriptions(
        [FromBody] DraftAchievementDescriptionsCommand command, DraftAchievementDescriptionsHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<AchievementDescriptionDto>, ProblemHttpResult>> AcceptAchievementDescription(
        [FromBody] AcceptAchievementDescriptionCommand command, AcceptAchievementDescriptionHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();
}
