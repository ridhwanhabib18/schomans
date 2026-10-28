using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SchoMans.Api.Auth;
using SchoMans.Api.Extensions;
using SchoMans.Application.Features.Attendance;
using SchoMans.Application.Features.Attendance.CloseSession;
using SchoMans.Application.Features.Attendance.ExportClassroomSummary;
using SchoMans.Application.Features.Attendance.GetClassroomSummary;
using SchoMans.Application.Features.Attendance.GetScanSettings;
using SchoMans.Application.Features.Attendance.GetSession;
using SchoMans.Application.Features.Attendance.GetStudentAttendance;
using SchoMans.Application.Features.Attendance.GetTodayLessons;
using SchoMans.Application.Features.Attendance.MarkAttendance;
using SchoMans.Application.Features.Attendance.OpenSession;
using SchoMans.Application.Features.Attendance.ScanQr;

namespace SchoMans.Api.Endpoints;

public static class AttendanceEndpoints
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        // Every signed-in role reaches the group; each endpoint narrows it, and the handlers check ownership.
        var group = app.MapGroup("/api/v1/attendance")
            .WithTags("Attendance")
            .RequireAuthorization(Policies.AnyRole)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/today", ListTodayLessons)
            .RequireAuthorization(Policies.Teacher)
            .WithName("ListTodayLessons");

        group.MapPost("/sessions", OpenSession)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<OpenSessionCommand>()
            .WithName("OpenAttendanceSession");

        group.MapGet("/sessions/{id:guid}", GetSession)
            .RequireAuthorization(Policies.StaffRead)
            .WithName("GetAttendanceSession");

        group.MapPost("/sessions/{id:guid}/close", CloseSession)
            .RequireAuthorization(Policies.Teacher)
            .WithName("CloseAttendanceSession");

        group.MapPut("/sessions/{sessionId:guid}/students/{studentId:guid}", MarkAttendance)
            .RequireAuthorization(Policies.Teacher)
            .WithValidation<MarkAttendanceCommand>()
            .WithName("MarkAttendance");

        group.MapPost("/scans", ScanQr)
            .RequireAuthorization(Policies.Student)
            .WithValidation<ScanQrCommand>()
            .WithName("ScanAttendanceQr");

        group.MapGet("/scan-settings", GetScanSettings)
            .RequireAuthorization(Policies.Student)
            .WithName("GetAttendanceScanSettings");

        group.MapGet("/classrooms/{classroomId:guid}/summary", GetClassroomSummary)
            .RequireAuthorization(Policies.StaffRead)
            .WithValidation<GetClassroomSummaryQuery>()
            .WithName("GetClassroomAttendanceSummary");

        group.MapGet("/classrooms/{classroomId:guid}/summary/export", ExportClassroomSummary)
            .RequireAuthorization(Policies.StaffRead)
            .WithValidation<GetClassroomSummaryQuery>()
            .Produces(StatusCodes.Status200OK, contentType: ExcelContentType)
            .WithName("ExportClassroomAttendanceSummary");

        group.MapGet("/students/me", GetMyAttendance)
            .RequireAuthorization(Policies.Student)
            .WithValidation<GetStudentAttendanceQuery>()
            .WithName("GetMyAttendance");

        group.MapGet("/students/{studentId:guid}", GetStudentAttendance)
            .WithValidation<GetStudentAttendanceQuery>()
            .WithName("GetStudentAttendance");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<TodayLessonDto>>, ProblemHttpResult>> ListTodayLessons(
        GetTodayLessonsHandler handler, CancellationToken ct)
        => (await handler.Handle(ct)).ToHttpResult();

    private static async Task<Results<Ok<AttendanceSessionDto>, ProblemHttpResult>> OpenSession(
        [FromBody] OpenSessionCommand command, OpenSessionHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<AttendanceSessionDto>, ProblemHttpResult>> GetSession(
        [FromRoute] Guid id, GetSessionHandler handler, CancellationToken ct)
        => (await handler.Handle(new GetSessionQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<AttendanceSessionDto>, ProblemHttpResult>> CloseSession(
        [FromRoute] Guid id, CloseSessionHandler handler, CancellationToken ct)
        => (await handler.Handle(new CloseSessionCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<SessionStudentDto>, ProblemHttpResult>> MarkAttendance(
        [FromRoute] Guid sessionId, [FromRoute] Guid studentId, [FromBody] MarkAttendanceCommand command, MarkAttendanceHandler handler, CancellationToken ct)
        => (await handler.Handle(command with { SessionId = sessionId, StudentId = studentId }, ct)).ToHttpResult();

    private static async Task<Results<Ok<ScanResultDto>, ProblemHttpResult>> ScanQr(
        [FromBody] ScanQrCommand command, ScanQrHandler handler, CancellationToken ct)
        => (await handler.Handle(command, ct)).ToHttpResult();

    private static Ok<ScanSettingsDto> GetScanSettings(GetScanSettingsHandler handler) => TypedResults.Ok(handler.Handle());

    private static async Task<Results<Ok<ClassroomAttendanceSummaryDto>, ProblemHttpResult>> GetClassroomSummary(
        [AsParameters] GetClassroomSummaryQuery query, GetClassroomSummaryHandler handler, CancellationToken ct)
        => (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ExportClassroomSummary(
        [AsParameters] GetClassroomSummaryQuery query, ExportClassroomSummaryHandler handler, CancellationToken ct)
    {
        var result = await handler.Handle(query, ct);
        return result.IsSuccess
            ? TypedResults.File(result.Value.Content, ExcelContentType, result.Value.FileName)
            : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<StudentAttendanceDto>, ProblemHttpResult>> GetMyAttendance(
        [AsParameters] GetStudentAttendanceQuery query, GetStudentAttendanceHandler handler, CancellationToken ct)
        => (await handler.Handle(query with { StudentId = null }, ct)).ToHttpResult();

    private static async Task<Results<Ok<StudentAttendanceDto>, ProblemHttpResult>> GetStudentAttendance(
        [FromRoute] Guid studentId, [AsParameters] GetStudentAttendanceQuery query, GetStudentAttendanceHandler handler, CancellationToken ct)
        => (await handler.Handle(query with { StudentId = studentId }, ct)).ToHttpResult();
}
