using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.TeacherNotes;
using SchoMans.Application.Features.TeacherNotes.CreateTeacherNote;
using SchoMans.Application.Features.TeacherNotes.DeleteTeacherNote;
using SchoMans.Application.Features.TeacherNotes.ListTeacherNotes;

namespace SchoMans.Api.Endpoints;

public static class TeacherNoteEndpoints
{
    public static IEndpointRouteBuilder MapTeacherNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/teacher-notes")
            .WithTags("Teacher notes")
            .RequireAuthorization(Policies.Teacher)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListTeacherNotes)
            .WithValidation<ListTeacherNotesQuery>()
            .WithName("ListTeacherNotes");

        group.MapPost("/", CreateTeacherNote)
            .WithValidation<CreateTeacherNoteCommand>()
            .WithName("CreateTeacherNote");

        group.MapDelete("/{id:guid}", DeleteTeacherNote)
            .WithName("DeleteTeacherNote");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<TeacherNoteDto>>, ProblemHttpResult>> ListTeacherNotes(
        [AsParameters] ListTeacherNotesQuery query, ListTeacherNotesHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<TeacherNoteDto>, ProblemHttpResult>> CreateTeacherNote(
        [FromBody] CreateTeacherNoteCommand command, CreateTeacherNoteHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteTeacherNote(
        [FromRoute] Guid id, DeleteTeacherNoteHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteTeacherNoteCommand(id), ct)).ToNoContentResult();
}
