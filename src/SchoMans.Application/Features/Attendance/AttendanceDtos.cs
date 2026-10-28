using SchoMans.Domain.Attendance;

namespace SchoMans.Application.Features.Attendance;

/// <param name="Token">What the QR code encodes (inside the check-in address). Only the teacher of the session gets it.</param>
/// <param name="NextRotationAt">Roughly when the next code appears; the screen shows a countdown.</param>
public sealed record QrCodeDto(string Token, DateTimeOffset RotatedAt, DateTimeOffset NextRotationAt);

/// <param name="Status">Null while the session is open and the student has not scanned yet.</param>
public sealed record SessionStudentDto(
    Guid StudentId,
    string FullName,
    string Nisn,
    AttendanceStatus? Status,
    DateTimeOffset? ScannedAt,
    bool IsManual,
    string? Note);

public sealed record AttendanceSessionDto(
    Guid Id,
    Guid ScheduleId,
    Guid ClassroomId,
    string ClassroomName,
    string SubjectName,
    string TeacherName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOpen,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    QrCodeDto? Qr,
    IReadOnlyList<SessionStudentDto> Students);

/// <summary>One lesson of the teacher today, with the state of its attendance session if one was opened.</summary>
public sealed record TodayLessonDto(
    Guid ScheduleId,
    Guid ClassroomId,
    string ClassroomName,
    string SubjectName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? SessionId,
    bool SessionOpen,
    int PresentCount,
    int StudentCount);

public sealed record ScanResultDto(
    Guid SessionId,
    string ClassroomName,
    string SubjectName,
    AttendanceStatus Status,
    DateTimeOffset? ScannedAt,
    bool AlreadyRecorded);

public sealed record ScanSettingsDto(bool GeofenceEnabled, double RadiusMeters);

public sealed record AttendanceCountsDto(int Present, int Excused, int Sick, int Absent, int Total, decimal PresentPercent)
{
    public static AttendanceCountsDto From(IReadOnlyCollection<AttendanceStatus> statuses)
    {
        var present = statuses.Count(s => s == AttendanceStatus.Present);
        var total = statuses.Count;
        return new AttendanceCountsDto(
            present,
            statuses.Count(s => s == AttendanceStatus.Excused),
            statuses.Count(s => s == AttendanceStatus.Sick),
            statuses.Count(s => s == AttendanceStatus.Absent),
            total,
            total == 0 ? 0 : Math.Round(100m * present / total, 1));
    }
}

public sealed record StudentAttendanceRowDto(Guid StudentId, string FullName, string Nisn, AttendanceCountsDto Counts);

/// <param name="Month">yyyy-MM.</param>
/// <param name="SessionCount">How many lessons of the classroom had attendance taken in the month.</param>
public sealed record ClassroomAttendanceSummaryDto(
    Guid ClassroomId,
    string ClassroomName,
    string Month,
    int SessionCount,
    IReadOnlyList<StudentAttendanceRowDto> Students);

public sealed record StudentAttendanceEntryDto(
    Guid SessionId,
    DateOnly Date,
    string SubjectName,
    TimeOnly StartTime,
    AttendanceStatus Status,
    DateTimeOffset? ScannedAt,
    string? Note);

public sealed record StudentAttendanceDto(
    Guid StudentId,
    string FullName,
    string ClassroomName,
    string Month,
    AttendanceCountsDto Counts,
    IReadOnlyList<StudentAttendanceEntryDto> Entries);
