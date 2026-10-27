using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Students;
using SchoMans.Application.Features.Students.CreateStudent;
using SchoMans.Application.Features.Students.DeleteStudent;
using SchoMans.Application.Features.Students.GetStudent;
using SchoMans.Application.Features.Students.GetStudentImportTemplate;
using SchoMans.Application.Features.Students.ImportStudents;
using SchoMans.Application.Features.Students.ListStudents;
using SchoMans.Application.Features.Students.UpdateStudent;

namespace SchoMans.Api.Endpoints;

public static class StudentEndpoints
{
    private const long ImportRequestLimit = 6 * 1024 * 1024; // a little above the 5 MB the use case accepts, for the form framing

    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/students")
            .WithTags("Students")
            .RequireAuthorization(Policies.StaffRead)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListStudents)
            .WithValidation<ListStudentsQuery>()
            .WithName("ListStudents");

        group.MapGet("/{id:guid}", GetStudent)
            .WithName("GetStudent");

        group.MapPost("/", CreateStudent)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<CreateStudentCommand>()
            .WithName("CreateStudent");

        group.MapPut("/{id:guid}", UpdateStudent)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<UpdateStudentCommand>()
            .WithName("UpdateStudent");

        group.MapDelete("/{id:guid}", DeleteStudent)
            .RequireAuthorization(Policies.Admin)
            .WithName("DeleteStudent");

        group.MapPost("/import", ImportStudents)
            .RequireAuthorization(Policies.Admin)
            .RequireRateLimiting(RateLimitPolicies.Uploads)
            .WithMetadata(new RequestSizeLimitAttribute(ImportRequestLimit))
            .DisableAntiforgery() // the API uses JWT bearer tokens, not cookies
            .WithName("ImportStudents");

        group.MapGet("/import-template", GetImportTemplate)
            .RequireAuthorization(Policies.Admin)
            .WithName("GetStudentImportTemplate");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<StudentDto>>, ProblemHttpResult>> ListStudents(
        [AsParameters] ListStudentsQuery query, ListStudentsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<StudentDto>, ProblemHttpResult>> GetStudent(
        [FromRoute] Guid id, GetStudentHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetStudentQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<StudentDto>, ProblemHttpResult>> CreateStudent(
        [FromBody] CreateStudentCommand command, CreateStudentHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(student => $"/api/v1/students/{student.Id}");

    private static async Task<Results<Ok<StudentDto>, ProblemHttpResult>> UpdateStudent(
        [FromRoute] Guid id, [FromBody] UpdateStudentCommand command, UpdateStudentHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteStudent(
        [FromRoute] Guid id, DeleteStudentHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteStudentCommand(id), ct)).ToNoContentResult();

    private static async Task<Results<Ok<ImportStudentsResult>, ProblemHttpResult>> ImportStudents(
        IFormFile file, ImportStudentsHandler handler, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return (await handler.Handle(new ImportStudentsCommand(stream, file.FileName, file.Length), ct)).ToHttpResult();
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> GetImportTemplate(
        GetStudentImportTemplateHandler handler, CancellationToken ct)
    {
        var result = await handler.Handle(new GetStudentImportTemplateQuery(), ct);
        return result.IsSuccess
            ? TypedResults.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : result.Error.ToProblem();
    }
}
