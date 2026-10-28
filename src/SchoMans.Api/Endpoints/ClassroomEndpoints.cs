using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Classrooms;
using SchoMans.Application.Features.Classrooms.CreateClassroom;
using SchoMans.Application.Features.Classrooms.DeleteClassroom;
using SchoMans.Application.Features.Classrooms.GetClassroom;
using SchoMans.Application.Features.Classrooms.ListClassrooms;
using SchoMans.Application.Features.Classrooms.UpdateClassroom;

namespace SchoMans.Api.Endpoints;

public static class ClassroomEndpoints
{
    public static IEndpointRouteBuilder MapClassroomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/classrooms")
            .WithTags("Classrooms")
            .RequireAuthorization(Policies.StaffRead)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListClassrooms)
            .WithValidation<ListClassroomsQuery>()
            .WithName("ListClassrooms");

        group.MapGet("/{id:guid}", GetClassroom)
            .WithName("GetClassroom");

        group.MapPost("/", CreateClassroom)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<CreateClassroomCommand>()
            .WithName("CreateClassroom");

        group.MapPut("/{id:guid}", UpdateClassroom)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<UpdateClassroomCommand>()
            .WithName("UpdateClassroom");

        group.MapDelete("/{id:guid}", DeleteClassroom)
            .RequireAuthorization(Policies.Admin)
            .WithName("DeleteClassroom");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<ClassroomDto>>, ProblemHttpResult>> ListClassrooms(
        [AsParameters] ListClassroomsQuery query, ListClassroomsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<ClassroomDto>, ProblemHttpResult>> GetClassroom(
        [FromRoute] Guid id, GetClassroomHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetClassroomQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<ClassroomDto>, ProblemHttpResult>> CreateClassroom(
        [FromBody] CreateClassroomCommand command, CreateClassroomHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(classroom => $"/api/v1/classrooms/{classroom.Id}");

    private static async Task<Results<Ok<ClassroomDto>, ProblemHttpResult>> UpdateClassroom(
        [FromRoute] Guid id, [FromBody] UpdateClassroomCommand command, UpdateClassroomHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteClassroom(
        [FromRoute] Guid id, DeleteClassroomHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteClassroomCommand(id), ct)).ToNoContentResult();
}
