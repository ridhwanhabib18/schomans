namespace SchoMans.Application.Features.Attendance.GetClassroomSummary;

/// <param name="Month">yyyy-MM, for example 2026-10.</param>
public sealed record GetClassroomSummaryQuery(Guid ClassroomId, string Month);
