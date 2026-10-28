namespace SchoMans.Application.Features.Schedules.UpdateSchedule;

/// <summary>The classroom of a lesson does not change; delete it and add it to the other classroom instead.</summary>
public sealed record UpdateScheduleCommand(Guid Id, Guid SubjectId, Guid TeacherId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, string RowVersion);
