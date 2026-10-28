using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Common;
using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.Assignments.CreateAssignment;
using SchoMans.Application.Features.Assignments.CreateAttachmentUploadUrl;
using SchoMans.Application.Features.Assignments.CreateSubmissionUploadUrl;
using SchoMans.Application.Features.Assignments.DeleteAssignment;
using SchoMans.Application.Features.Assignments.GetAssignment;
using SchoMans.Application.Features.Assignments.GetAttachmentUrl;
using SchoMans.Application.Features.Assignments.GetSubmission;
using SchoMans.Application.Features.Assignments.GetSubmissionFileUrl;
using SchoMans.Application.Features.Assignments.GradeSubmission;
using SchoMans.Application.Features.Assignments.ListAssignments;
using SchoMans.Application.Features.Assignments.ListStudentAssignments;
using SchoMans.Application.Features.Assignments.ListSubmissions;
using SchoMans.Application.Features.Assignments.ListTeachingOptions;
using SchoMans.Application.Features.Assignments.SubmitAssignment;
using SchoMans.Application.Features.Assignments.UpdateAssignment;
using SchoMans.Application.Features.Files.CreateUploadUrl;

namespace SchoMans.Api.Endpoints;

public static class AssignmentEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentEndpoints(this IEndpointRouteBuilder app)
    {
        // Teachers give assignments, students hand in work, parents and the principal read. The handlers check
        // that the assignment is the teacher's own, or given to the student's classroom, or to the parent's child.
        var group = app.MapGroup("/api/v1/assignments")
            .WithTags("Assignments")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListAssignments)
            .RequireAuthorization(Policies.TeachingStaff)
            .WithValidation<ListAssignmentsQuery>()
            .WithName("ListAssignments");

        group.MapGet("/teaching-options", ListTeachingOptions)
            .RequireAuthorization(Policies.Teacher)
            .WithName("ListTeachingOptions");

        group.MapPost("/attachments/upload-url", CreateAttachmentUploadUrl)
            .RequireAuthorization(Policies.Teacher)
            .RequireRateLimiting(RateLimitPolicies.Uploads)
            .WithValidation<CreateAttachmentUploadUrlCommand>()
            .WithName("CreateAssignmentAttachmentUploadUrl");

        group.MapPost("/", CreateAssignment)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<CreateAssignmentCommand>()
            .WithName("CreateAssignment");

        group.MapGet("/mine", ListMyAssignments)
            .RequireAuthorization(Policies.Student)
            .WithValidation<ListStudentAssignmentsQuery>()
            .WithName("ListMyAssignments");

        group.MapGet("/children/{studentId:guid}", ListChildAssignments)
            .RequireAuthorization(Policies.Parent)
            .WithValidation<ListStudentAssignmentsQuery>()
            .WithName("ListChildAssignments");

        group.MapGet("/{id:guid}", GetAssignment)
            .WithName("GetAssignment");

        group.MapPut("/{id:guid}", UpdateAssignment)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<UpdateAssignmentCommand>()
            .WithName("UpdateAssignment");

        group.MapDelete("/{id:guid}", DeleteAssignment)
            .RequireAuthorization(Policies.Teacher)
            .WithName("DeleteAssignment");

        group.MapGet("/{id:guid}/attachments/{attachmentId:guid}/download-url", GetAttachmentUrl)
            .WithName("GetAssignmentAttachmentUrl");

        group.MapPost("/{id:guid}/submission/upload-url", CreateSubmissionUploadUrl)
            .RequireAuthorization(Policies.Student)
            .RequireRateLimiting(RateLimitPolicies.Uploads)
            .WithValidation<CreateSubmissionUploadUrlCommand>()
            .WithName("CreateSubmissionUploadUrl");

        group.MapPut("/{id:guid}/submission", SubmitAssignment)
            .RequireAuthorization(Policies.Student)
            .WithValidation<SubmitAssignmentCommand>()
            .WithName("SubmitAssignment");

        group.MapGet("/{id:guid}/submissions", ListSubmissions)
            .RequireAuthorization(Policies.TeachingStaff)
            .WithName("ListSubmissions");

        group.MapGet("/submissions/{submissionId:guid}", GetSubmission)
            .WithName("GetSubmission");

        group.MapGet("/submissions/{submissionId:guid}/download-url", GetSubmissionFileUrl)
            .WithName("GetSubmissionFileUrl");

        group.MapPut("/submissions/{submissionId:guid}/score", GradeSubmission)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<GradeSubmissionCommand>()
            .WithName("GradeSubmission");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<AssignmentListItemDto>>, ProblemHttpResult>> ListAssignments(
        [AsParameters] ListAssignmentsQuery query, ListAssignmentsHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<TeachingOptionDto>>, ProblemHttpResult>> ListTeachingOptions(
        ListTeachingOptionsHandler handler, CancellationToken ct)
        => (await handler.Handle(ct)).ToHttpResult();

    private static async Task<Results<Ok<UploadUrlDto>, ProblemHttpResult>> CreateAttachmentUploadUrl(
        [FromBody] CreateAttachmentUploadUrlCommand command, CreateAttachmentUploadUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Created<AssignmentDto>, ProblemHttpResult>> CreateAssignment(
        [FromBody] CreateAssignmentCommand command, CreateAssignmentHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToCreatedResult(assignment => $"/api/v1/assignments/{assignment.Id}");

    private static async Task<Results<Ok<PagedResult<StudentAssignmentDto>>, ProblemHttpResult>> ListMyAssignments(
        [AsParameters] ListStudentAssignmentsQuery query, ListStudentAssignmentsHandler handler, CancellationToken ct)
        => (await handler.Handle(query with { StudentId = null }, ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<StudentAssignmentDto>>, ProblemHttpResult>> ListChildAssignments(
        [FromRoute] Guid studentId, [AsParameters] ListStudentAssignmentsQuery query, ListStudentAssignmentsHandler handler, CancellationToken ct)
        => (await handler.Handle(query with { StudentId = studentId }, ct)).ToHttpResult();

    private static async Task<Results<Ok<AssignmentDto>, ProblemHttpResult>> GetAssignment(
        [FromRoute] Guid id, [FromQuery] Guid? studentId, GetAssignmentHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetAssignmentQuery(id, studentId), ct)).ToHttpResult();

    private static async Task<Results<Ok<AssignmentDto>, ProblemHttpResult>> UpdateAssignment(
        [FromRoute] Guid id, [FromBody] UpdateAssignmentCommand command, UpdateAssignmentHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { Id = id }, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAssignment(
        [FromRoute] Guid id, DeleteAssignmentHandler handler, CancellationToken ct)
        => (await handler.Handle(new DeleteAssignmentCommand(id), ct)).ToNoContentResult();

    private static async Task<Results<Ok<DownloadUrlDto>, ProblemHttpResult>> GetAttachmentUrl(
        [FromRoute] Guid id, [FromRoute] Guid attachmentId, [FromQuery] Guid? studentId, GetAttachmentUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetAttachmentUrlQuery(id, attachmentId, studentId), ct)).ToHttpResult();

    private static async Task<Results<Ok<UploadUrlDto>, ProblemHttpResult>> CreateSubmissionUploadUrl(
        [FromRoute] Guid id, [FromBody] CreateSubmissionUploadUrlCommand command, CreateSubmissionUploadUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { AssignmentId = id }, ct)).ToHttpResult();

    private static async Task<Results<Ok<SubmissionDto>, ProblemHttpResult>> SubmitAssignment(
        [FromRoute] Guid id, [FromBody] SubmitAssignmentCommand command, SubmitAssignmentHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { AssignmentId = id }, ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<SubmissionRowDto>>, ProblemHttpResult>> ListSubmissions(
        [FromRoute] Guid id, ListSubmissionsHandler handler, CancellationToken ct)
        => (await handler.Handle(new ListSubmissionsQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<SubmissionDto>, ProblemHttpResult>> GetSubmission(
        [FromRoute] Guid submissionId, GetSubmissionHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetSubmissionQuery(submissionId), ct)).ToHttpResult();

    private static async Task<Results<Ok<DownloadUrlDto>, ProblemHttpResult>> GetSubmissionFileUrl(
        [FromRoute] Guid submissionId, GetSubmissionFileUrlHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetSubmissionFileUrlQuery(submissionId), ct)).ToHttpResult();

    private static async Task<Results<Ok<SubmissionDto>, ProblemHttpResult>> GradeSubmission(
        [FromRoute] Guid submissionId, [FromBody] GradeSubmissionCommand command, GradeSubmissionHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { SubmissionId = submissionId }, ct)).ToHttpResult();
}
