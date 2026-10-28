using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Schedules;
using SchoMans.Application.Features.Schedules.CreateSchedule;
using SchoMans.Application.Features.Schedules.DeleteSchedule;
using SchoMans.Application.Features.Schedules.ListSchedules;
using SchoMans.Application.Features.Schedules.UpdateSchedule;

namespace SchoMans.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/schedules")
            .WithTags("Schedules")
            .RequireAuthorization(Policies.StaffRead)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListSchedules)
            .WithValidation<ListSchedulesQuery>()
            .WithName("ListSchedules");

        group.MapPost("/", CreateSchedule)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<CreateScheduleCommand>()
            .WithName("CreateSchedule");

        group.MapPut("/{id:guid}", UpdateSchedule)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<UpdateScheduleCommand>()
            .WithName("UpdateSchedule");

        group.MapDelete("/{id:guid}", DeleteSchedule)
            .RequireAuthorization(Policies.Admin)
            .WithName("DeleteSchedule");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<ScheduleDto>>, ProblemHttpResult>> ListSchedules(
        [AsParameters] ListSchedulesQuery query, ListSchedulesHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Created<ScheduleDto>, ProblemHttpResult>> CreateSchedule(
        [FromBody] CreateScheduleCommand command, CreateScheduleHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(schedule => $"/api/v1/schedules/{schedule.Id}");

    private static async Task<Results<Ok<ScheduleDto>, ProblemHttpResult>> UpdateSchedule(
        [FromRoute] Guid id, [FromBody] UpdateScheduleCommand command, UpdateScheduleHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSchedule(
        [FromRoute] Guid id, DeleteScheduleHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteScheduleCommand(id), ct)).ToNoContentResult();
}
