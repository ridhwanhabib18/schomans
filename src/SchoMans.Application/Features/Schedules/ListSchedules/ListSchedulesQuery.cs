namespace SchoMans.Application.Features.Schedules.ListSchedules;

/// <summary>The timetable, ordered by day and start time. Filter by classroom, teacher or day.</summary>
public sealed record ListSchedulesQuery(
    int Page = 1,
    int PageSize = 100,
    Guid? ClassroomId = null,
    Guid? TeacherId = null,
    DayOfWeek? DayOfWeek = null);
