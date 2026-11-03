namespace SchoMans.Application.Features.PrincipalAssistant;

/// <summary>The functions the assistant may call. Each one is read-only and answers from the school's own data.</summary>
public static class AssistantToolNames
{
    public const string ListClassrooms = "list_classrooms";
    public const string AttendanceByClassroom = "attendance_by_classroom";
    public const string AttendanceTrend = "attendance_trend";
    public const string StudentsLowAttendance = "students_low_attendance";
    public const string AverageGradesBySubject = "average_grades_by_subject";
    public const string PendingRequests = "pending_requests";
    public const string AdmissionApplicants = "admission_applicants";
    public const string AssignmentSummary = "assignment_summary";
}

/// <param name="Role"><c>user</c> or <c>assistant</c>.</param>
public sealed record AssistantTurnDto(string Role, string Text);

/// <summary>One number the answer rests on.</summary>
public sealed record AssistantFigureDto(string Label, string Value);

/// <param name="Url">The page of the app where the principal can look at the same numbers; null when the principal has no such page.</param>
public sealed record AssistantSourceDto(string Tool, string Label, string? Url, List<AssistantFigureDto> Figures);

public sealed record AssistantAnswerDto(string Answer, List<AssistantSourceDto> Sources);
