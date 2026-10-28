namespace SchoMans.Application.Features.Attendance.GetStudentAttendance;

/// <param name="StudentId">Null for the signed-in student themselves.</param>
/// <param name="Month">yyyy-MM.</param>
public sealed record GetStudentAttendanceQuery(string Month, Guid? StudentId = null);
