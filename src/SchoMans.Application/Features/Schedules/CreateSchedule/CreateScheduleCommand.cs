namespace SchoMans.Application.Features.Schedules.CreateSchedule;

public sealed record CreateScheduleCommand(Guid ClassroomId, Guid SubjectId, Guid TeacherId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
