using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Teachers;
using SchoMans.Application.Features.Teachers.CreateTeacher;
using SchoMans.Application.Features.Teachers.DeleteTeacher;
using SchoMans.Application.Features.Teachers.GetTeacher;
using SchoMans.Application.Features.Teachers.ListTeachers;
using SchoMans.Application.Features.Teachers.UpdateTeacher;

namespace SchoMans.Api.Endpoints;

public static class TeacherEndpoints
{
    public static IEndpointRouteBuilder MapTeacherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/teachers")
            .WithTags("Teachers")
            .RequireAuthorization(Policies.Editors)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListTeachers)
            .WithValidation<ListTeachersQuery>()
            .WithName("ListTeachers");

        group.MapGet("/{id:guid}", GetTeacher)
            .WithName("GetTeacher");

        group.MapPost("/", CreateTeacher)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<CreateTeacherCommand>()
            .WithName("CreateTeacher");

        group.MapPut("/{id:guid}", UpdateTeacher)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<UpdateTeacherCommand>()
            .WithName("UpdateTeacher");

        group.MapDelete("/{id:guid}", DeleteTeacher)
            .RequireAuthorization(Policies.Admin)
            .WithName("DeleteTeacher");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<TeacherDto>>, ProblemHttpResult>> ListTeachers(
        [AsParameters] ListTeachersQuery query, ListTeachersHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<TeacherDto>, ProblemHttpResult>> GetTeacher(
        [FromRoute] Guid id, GetTeacherHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetTeacherQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<TeacherDto>, ProblemHttpResult>> CreateTeacher(
        [FromBody] CreateTeacherCommand command, CreateTeacherHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(teacher => $"/api/v1/teachers/{teacher.Id}");

    private static async Task<Results<Ok<TeacherDto>, ProblemHttpResult>> UpdateTeacher(
        [FromRoute] Guid id, [FromBody] UpdateTeacherCommand command, UpdateTeacherHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteTeacher(
        [FromRoute] Guid id, DeleteTeacherHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteTeacherCommand(id), ct)).ToNoContentResult();
}
