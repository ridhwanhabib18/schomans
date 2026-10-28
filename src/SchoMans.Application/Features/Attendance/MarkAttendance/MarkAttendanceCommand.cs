using SchoMans.Domain.Attendance;

namespace SchoMans.Application.Features.Attendance.MarkAttendance;

/// <summary>The teacher sets a student's status by hand: Present for a phone without a camera, Sick or Excused with a note.</summary>
public sealed record MarkAttendanceCommand(Guid SessionId, Guid StudentId, AttendanceStatus Status, string? Note);
