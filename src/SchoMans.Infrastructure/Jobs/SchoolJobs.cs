using Hangfire;
using SchoMans.Application.Features.Assignments.SendDueReminders;
using SchoMans.Application.Features.Attendance.CloseStaleSessions;
using SchoMans.Application.Features.Attendance.RotateQrCodes;

namespace SchoMans.Infrastructure.Jobs;

// The recurring jobs are thin: each calls one Application handler in its own DI scope (Hangfire.AspNetCore creates it).

/// <summary>Every minute: a new QR secret for every open attendance session.</summary>
[AutomaticRetry(Attempts = 0)] // the next run is a minute away; a retry would only rotate twice
[DisableConcurrentExecution(timeoutInSeconds: 50)]
public sealed class RotateAttendanceQrJob(RotateQrCodesHandler handler)
{
    public Task RunAsync(CancellationToken ct) => handler.Handle(ct);
}

/// <summary>Every 15 minutes: close sessions left open after their lesson and mark the students who did not scan Absent.</summary>
[DisableConcurrentExecution(timeoutInSeconds: 600)]
public sealed class CloseStaleSessionsJob(CloseStaleSessionsHandler handler)
{
    public Task RunAsync(CancellationToken ct) => handler.Handle(ct);
}

/// <summary>Every morning at 07:00 school time: remind students of assignments due tomorrow.</summary>
[DisableConcurrentExecution(timeoutInSeconds: 600)]
public sealed class AssignmentDueReminderJob(SendDueRemindersHandler handler)
{
    public Task RunAsync(CancellationToken ct) => handler.Handle(ct);
}
