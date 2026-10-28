using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Subjects;
using SchoMans.Application.Features.Subjects.CreateSubject;
using SchoMans.Application.Features.Subjects.DeleteSubject;
using SchoMans.Application.Features.Subjects.GetSubject;
using SchoMans.Application.Features.Subjects.ListSubjects;
using SchoMans.Application.Features.Subjects.UpdateSubject;

namespace SchoMans.Api.Endpoints;

public static class SubjectEndpoints
{
    public static IEndpointRouteBuilder MapSubjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/subjects")
            .WithTags("Subjects")
            .RequireAuthorization(Policies.StaffRead)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListSubjects)
            .WithValidation<ListSubjectsQuery>()
            .WithName("ListSubjects");

        group.MapGet("/{id:guid}", GetSubject)
            .WithName("GetSubject");

        group.MapPost("/", CreateSubject)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<CreateSubjectCommand>()
            .WithName("CreateSubject");

        group.MapPut("/{id:guid}", UpdateSubject)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<UpdateSubjectCommand>()
            .WithName("UpdateSubject");

        group.MapDelete("/{id:guid}", DeleteSubject)
            .RequireAuthorization(Policies.Admin)
            .WithName("DeleteSubject");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<SubjectDto>>, ProblemHttpResult>> ListSubjects(
        [AsParameters] ListSubjectsQuery query, ListSubjectsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<SubjectDto>, ProblemHttpResult>> GetSubject(
        [FromRoute] Guid id, GetSubjectHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetSubjectQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<SubjectDto>, ProblemHttpResult>> CreateSubject(
        [FromBody] CreateSubjectCommand command, CreateSubjectHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(subject => $"/api/v1/subjects/{subject.Id}");

    private static async Task<Results<Ok<SubjectDto>, ProblemHttpResult>> UpdateSubject(
        [FromRoute] Guid id, [FromBody] UpdateSubjectCommand command, UpdateSubjectHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSubject(
        [FromRoute] Guid id, DeleteSubjectHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteSubjectCommand(id), ct)).ToNoContentResult();
}
