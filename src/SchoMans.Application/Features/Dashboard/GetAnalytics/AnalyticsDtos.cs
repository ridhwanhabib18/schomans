namespace SchoMans.Application.Features.Dashboard.GetAnalytics;

/// <summary>The analytics of the dashboard (F-09). Only the part of the signed-in role is filled.</summary>
public sealed record AnalyticsDto(
    string Role,
    PrincipalAnalyticsDto? Principal,
    TeacherAnalyticsDto? Teacher,
    List<ChildAnalyticsDto> Children);

public sealed record PrincipalAnalyticsDto(List<ClassroomAttendanceTrendDto> AttendanceTrend, List<SubjectAverageDto> SubjectAverages);

public sealed record ClassroomAttendanceTrendDto(Guid ClassroomId, string ClassroomName, List<MonthRateDto> Months);

/// <param name="Rate">Percent of the attendance records of the month that are present; null in a month without records.</param>
public sealed record MonthRateDto(string Month, decimal? Rate);

/// <param name="Average">Average of the final grades of the students in the active term.</param>
public sealed record SubjectAverageDto(Guid SubjectId, string SubjectName, decimal Average, int Students);

public sealed record TeacherAnalyticsDto(decimal AttendanceThreshold, List<AttentionStudentDto> Flagged);

/// <param name="AttendanceRate">Percent present in the active term; null without records.</param>
/// <param name="LowAttendance">The attendance is below the threshold.</param>
/// <param name="BelowPassingSubjects">Subjects whose final grade is below the minimum passing score.</param>
public sealed record AttentionStudentDto(
    Guid StudentId,
    string StudentName,
    string ClassroomName,
    decimal? AttendanceRate,
    bool LowAttendance,
    List<string> BelowPassingSubjects);

public sealed record ChildAnalyticsDto(Guid StudentId, string StudentName, List<ChildMonthDto> Months);

/// <param name="AverageGrade">Average of the published scores recorded in the month; null when there are none.</param>
public sealed record ChildMonthDto(string Month, decimal? AverageGrade, decimal? AttendanceRate);
