namespace SchoMans.Application.Features.Attendance.OpenSession;

/// <summary>Opens today's attendance for one of the teacher's lessons. Opening it again returns the same open session.</summary>
public sealed record OpenSessionCommand(Guid ScheduleId);
