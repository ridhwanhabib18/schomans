using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Grading.EnterGrade;
using SchoMans.Application.Features.Grading.GetGradeBook;
using SchoMans.Application.Features.Grading.PublishGrades;
using SchoMans.Application.Features.Grading.UnpublishGrades;

namespace SchoMans.Api.Endpoints;

public static class GradeEndpoints
{
    public static IEndpointRouteBuilder MapGradeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/grades")
            .WithTags("Grades")
            .RequireAuthorization(Policies.TeachingStaff)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/book", GetGradeBook)
            .WithName("GetGradeBook");

        group.MapPut("/", EnterGrade)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<EnterGradeCommand>()
            .WithName("EnterGrade");

        group.MapPost("/publications", PublishGrades)
            .RequireAuthorization(Policies.Teacher)
            .WithName("PublishGrades");

        group.MapDelete("/publications", UnpublishGrades)
            .RequireAuthorization(Policies.Teacher)
            .WithName("UnpublishGrades");

        return app;
    }

    private static async Task<Results<Ok<GradeBookDto>, ProblemHttpResult>> GetGradeBook(
        [AsParameters] GetGradeBookQuery query, GetGradeBookHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<EnteredGradeDto>, ProblemHttpResult>> EnterGrade(
        [FromBody] EnterGradeCommand command, EnterGradeHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<GradePublicationDto>, ProblemHttpResult>> PublishGrades(
        [FromBody] PublishGradesCommand command, PublishGradesHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> UnpublishGrades(
        [AsParameters] UnpublishGradesCommand command, UnpublishGradesHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToNoContentResult();
}
