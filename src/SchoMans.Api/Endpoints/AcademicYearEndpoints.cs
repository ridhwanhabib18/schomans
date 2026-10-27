using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.AcademicYears;
using SchoMans.Application.Features.AcademicYears.ActivateAcademicYear;
using SchoMans.Application.Features.AcademicYears.CreateAcademicYear;
using SchoMans.Application.Features.AcademicYears.GetAcademicYear;
using SchoMans.Application.Features.AcademicYears.ListAcademicYears;
using SchoMans.Application.Features.AcademicYears.RolloverAcademicYear;
using SchoMans.Application.Features.AcademicYears.UpdateAcademicYear;

namespace SchoMans.Api.Endpoints;

public static class AcademicYearEndpoints
{
    public static IEndpointRouteBuilder MapAcademicYearEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/academic-years")
            .WithTags("AcademicYears")
            .RequireAuthorization(Policies.StaffRead)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListAcademicYears)
            .WithValidation<ListAcademicYearsQuery>()
            .WithName("ListAcademicYears");

        group.MapGet("/{id:guid}", GetAcademicYear)
            .WithName("GetAcademicYear");

        group.MapPost("/", CreateAcademicYear)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<CreateAcademicYearCommand>()
            .WithName("CreateAcademicYear");

        group.MapPut("/{id:guid}", UpdateAcademicYear)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<UpdateAcademicYearCommand>()
            .WithName("UpdateAcademicYear");

        group.MapPost("/{id:guid}/activate", ActivateAcademicYear)
            .RequireAuthorization(Policies.Admin)
            .WithName("ActivateAcademicYear");

        group.MapPost("/rollover", RolloverAcademicYear)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<RolloverAcademicYearCommand>()
            .WithName("RolloverAcademicYear");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<AcademicYearDto>>, ProblemHttpResult>> ListAcademicYears(
        [AsParameters] ListAcademicYearsQuery query, ListAcademicYearsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AcademicYearDto>, ProblemHttpResult>> GetAcademicYear(
        [FromRoute] Guid id, GetAcademicYearHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetAcademicYearQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<AcademicYearDto>, ProblemHttpResult>> CreateAcademicYear(
        [FromBody] CreateAcademicYearCommand command, CreateAcademicYearHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(year => $"/api/v1/academic-years/{year.Id}");

    private static async Task<Results<Ok<AcademicYearDto>, ProblemHttpResult>> UpdateAcademicYear(
        [FromRoute] Guid id, [FromBody] UpdateAcademicYearCommand command, UpdateAcademicYearHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<Ok<AcademicYearDto>, ProblemHttpResult>> ActivateAcademicYear(
        [FromRoute] Guid id, ActivateAcademicYearHandler handler, CancellationToken ct)
        => (await handler.Handle(new ActivateAcademicYearCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<RolloverResult>, ProblemHttpResult>> RolloverAcademicYear(
        [FromBody] RolloverAcademicYearCommand command, RolloverAcademicYearHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();
}
