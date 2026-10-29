using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Admissions;
using SchoMans.Application.Features.Admissions.ApplicantDocuments;
using SchoMans.Application.Features.Admissions.ConfirmReregistration;
using SchoMans.Application.Features.Admissions.GetMyAdmission;
using SchoMans.Application.Features.Admissions.GetOpenAdmission;
using SchoMans.Application.Features.Admissions.Periods;
using SchoMans.Application.Features.Admissions.SaveApplicantForm;
using SchoMans.Application.Features.Admissions.Staff;
using SchoMans.Application.Features.Admissions.StartApplication;
using SchoMans.Application.Features.Admissions.SubmitApplication;
using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.Files.CreateUploadUrl;
using SchoMans.Domain.Admissions;

namespace SchoMans.Api.Endpoints;

public static class AdmissionEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admissions")
            .WithTags("Admissions")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // The public side: the banner of the landing page. AllowAnonymous overrides the group's policy.
        group.MapGet("/open", GetOpenAdmission)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Public)
            .WithName("GetOpenAdmission");

        // The applicant: their own application, step by step.
        group.MapGet("/me", GetMyAdmission)
            .RequireAuthorization(Policies.Applicant)
            .WithName("GetMyAdmission");

        group.MapPost("/me", StartApplication)
            .RequireAuthorization(Policies.Applicant)
            .WithName("StartApplication");

        group.MapPut("/me/form", SaveApplicantForm)
            .RequireAuthorization(Policies.Applicant)
            .WithValidation<SaveApplicantFormCommand>()
            .WithName("SaveApplicantForm");

        group.MapPost("/me/documents/upload-url", CreateDocumentUploadUrl)
            .RequireAuthorization(Policies.Applicant)
            .WithValidation<CreateDocumentUploadUrlCommand>()
            .WithName("CreateApplicantDocumentUploadUrl");

        group.MapPut("/me/documents", AttachDocument)
            .RequireAuthorization(Policies.Applicant)
            .WithValidation<AttachDocumentCommand>()
            .WithName("AttachApplicantDocument");

        group.MapPost("/me/submit", SubmitApplication)
            .RequireAuthorization(Policies.Applicant)
            .WithName("SubmitApplication");

        group.MapPost("/me/reregistration", ConfirmReregistration)
            .RequireAuthorization(Policies.Applicant)
            .WithName("ConfirmReregistration");

        // The admin staff: periods, verification, decisions and enrollment.
        group.MapGet("/periods", ListPeriods)
            .RequireAuthorization(Policies.Admin)
            .WithName("ListAdmissionPeriods");

        group.MapPost("/periods", CreatePeriod)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<CreateAdmissionPeriodCommand>()
            .WithName("CreateAdmissionPeriod");

        group.MapPut("/periods/{id:guid}", UpdatePeriod)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<UpdateAdmissionPeriodCommand>()
            .WithName("UpdateAdmissionPeriod");

        group.MapDelete("/periods/{id:guid}", DeletePeriod)
            .RequireAuthorization(Policies.Admin)
            .WithName("DeleteAdmissionPeriod");

        group.MapGet("/applicants", ListApplicants)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<ListApplicantsQuery>()
            .WithName("ListApplicants");

        group.MapGet("/applicants/{id:guid}", GetApplicant)
            .RequireAuthorization(Policies.Admin)
            .WithName("GetApplicant");

        group.MapGet("/applicants/{id:guid}/documents/{type}/url", GetApplicantDocumentUrl)
            .RequireAuthorization(Policies.Admin)
            .WithName("GetApplicantDocumentUrl");

        group.MapPut("/applicants/{id:guid}/documents/{type}/review", ReviewDocument)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<ReviewDocumentCommand>()
            .WithName("ReviewApplicantDocument");

        group.MapPost("/applicants/{id:guid}/decision", DecideApplicant)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<DecideApplicantCommand>()
            .WithName("DecideApplicant");

        group.MapPost("/applicants/{id:guid}/enroll", EnrollApplicant)
            .RequireAuthorization(Policies.Admin)
            .WithValidation<EnrollApplicantCommand>()
            .WithName("EnrollApplicant");

        return app;
    }

    private static async Task<Results<Ok<OpenAdmissionDto>, ProblemHttpResult>> GetOpenAdmission(
        GetOpenAdmissionHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetOpenAdmissionQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<MyAdmissionDto>, ProblemHttpResult>> GetMyAdmission(
        GetMyAdmissionHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetMyAdmissionQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<MyApplicationDto>, ProblemHttpResult>> StartApplication(
        StartApplicationHandler handler, CancellationToken ct)
        => (await handler.Handle(new StartApplicationCommand(), ct)).ToHttpResult();

    private static async Task<Results<Ok<MyApplicationDto>, ProblemHttpResult>> SaveApplicantForm(
        [FromBody] SaveApplicantFormCommand command, SaveApplicantFormHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<UploadUrlDto>, ProblemHttpResult>> CreateDocumentUploadUrl(
        [FromBody] CreateDocumentUploadUrlCommand command, CreateDocumentUploadUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<MyApplicationDto>, ProblemHttpResult>> AttachDocument(
        [FromBody] AttachDocumentCommand command, AttachDocumentHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<MyApplicationDto>, ProblemHttpResult>> SubmitApplication(
        SubmitApplicationHandler handler, CancellationToken ct)
        => (await handler.Handle(new SubmitApplicationCommand(), ct)).ToHttpResult();

    private static async Task<Results<Ok<MyApplicationDto>, ProblemHttpResult>> ConfirmReregistration(
        ConfirmReregistrationHandler handler, CancellationToken ct)
        => (await handler.Handle(new ConfirmReregistrationCommand(), ct)).ToHttpResult();

    private static async Task<Results<Ok<List<AdmissionPeriodDto>>, ProblemHttpResult>> ListPeriods(
        ListAdmissionPeriodsHandler handler, CancellationToken ct)
        => (await handler.Handle(new ListAdmissionPeriodsQuery(), ct)).ToHttpResult();

    private static async Task<Results<Created<AdmissionPeriodDto>, ProblemHttpResult>> CreatePeriod(
        [FromBody] CreateAdmissionPeriodCommand command, CreateAdmissionPeriodHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(period => $"/api/v1/admissions/periods/{period.Id}");

    private static async Task<Results<Ok<AdmissionPeriodDto>, ProblemHttpResult>> UpdatePeriod(
        [FromRoute] Guid id, [FromBody] UpdateAdmissionPeriodCommand command, UpdateAdmissionPeriodHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeletePeriod(
        [FromRoute] Guid id, DeleteAdmissionPeriodHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteAdmissionPeriodCommand(id), ct)).ToNoContentResult();

    private static async Task<Results<Ok<PagedResult<ApplicantListItemDto>>, ProblemHttpResult>> ListApplicants(
        [AsParameters] ListApplicantsQuery query, ListApplicantsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicantDetailDto>, ProblemHttpResult>> GetApplicant(
        [FromRoute] Guid id, GetApplicantHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetApplicantQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<DownloadUrlDto>, ProblemHttpResult>> GetApplicantDocumentUrl(
        [FromRoute] Guid id, [FromRoute] DocumentType type, GetApplicantDocumentUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetApplicantDocumentUrlQuery(id, type), ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicantDetailDto>, ProblemHttpResult>> ReviewDocument(
        [FromRoute] Guid id, [FromRoute] DocumentType type, [FromBody] ReviewDocumentCommand command, ReviewDocumentHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { ApplicantId = id, Type = type }, ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicantDetailDto>, ProblemHttpResult>> DecideApplicant(
        [FromRoute] Guid id, [FromBody] DecideApplicantCommand command, DecideApplicantHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { ApplicantId = id }, ct)).ToHttpResult();

    private static async Task<Results<Ok<ApplicantDetailDto>, ProblemHttpResult>> EnrollApplicant(
        [FromRoute] Guid id, [FromBody] EnrollApplicantCommand command, EnrollApplicantHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { ApplicantId = id }, ct)).ToHttpResult();
}
