using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.ReportCards;
using SchoMans.Application.Features.ReportCards.GenerateReportCards;
using SchoMans.Application.Features.ReportCards.GetReportCardUrl;
using SchoMans.Application.Features.ReportCards.GetStudentGrades;
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

        group.MapGet("/url", GetReportCardUrl)
            .WithName("GetReportCardUrl");

        group.MapPut("/descriptions", WriteAchievementDescription)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<WriteAchievementDescriptionCommand>()
            .WithName("WriteAchievementDescription");

        group.MapPost("/generate", GenerateReportCards)
            .RequireAuthorization(Policies.Teacher)
            .WithName("GenerateReportCards");

        return app;
    }

    private static async Task<Results<Ok<StudentGradesDto>, ProblemHttpResult>> GetStudentGrades(
        [AsParameters] GetStudentGradesQuery query, GetStudentGradesHandler handler, CancellationToken ct)
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
}
