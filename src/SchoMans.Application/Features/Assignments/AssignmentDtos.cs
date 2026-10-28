namespace SchoMans.Application.Features.Assignments;

public sealed record AssignmentClassroomDto(Guid Id, string Name);

public sealed record AttachmentDto(Guid Id, string FileName);

/// <param name="MySubmission">For a student: their own work; for a parent: the work of the chosen child. Null for staff.</param>
/// <param name="CanEdit">True for the teacher who gave the assignment.</param>
public sealed record AssignmentDto(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset DueAt,
    Guid SubjectId,
    string SubjectName,
    Guid TeacherId,
    string TeacherName,
    string TermName,
    IReadOnlyList<AssignmentClassroomDto> Classrooms,
    IReadOnlyList<AttachmentDto> Attachments,
    SubmissionDto? MySubmission,
    bool CanEdit,
    DateTimeOffset CreatedAt,
    string RowVersion);

/// <summary>A row of the teacher's list: how far the class is with the work.</summary>
public sealed record AssignmentListItemDto(
    Guid Id,
    string Title,
    string SubjectName,
    IReadOnlyList<string> ClassroomNames,
    DateTimeOffset DueAt,
    int StudentCount,
    int SubmittedCount,
    int GradedCount);

public enum StudentAssignmentStatus
{
    NotSubmitted,
    Submitted,
    Late,
}

/// <param name="IsOverdue">Not handed in and the due date has passed.</param>
public sealed record StudentAssignmentDto(
    Guid Id,
    string Title,
    string SubjectName,
    string TeacherName,
    DateTimeOffset DueAt,
    StudentAssignmentStatus Status,
    bool IsOverdue,
    DateTimeOffset? SubmittedAt,
    decimal? Score);

public sealed record SubmissionDto(
    Guid Id,
    Guid AssignmentId,
    Guid StudentId,
    string StudentName,
    string? FileName,
    bool HasFile,
    string? TextBody,
    DateTimeOffset SubmittedAt,
    bool IsLate,
    decimal? Score,
    string? Feedback,
    DateTimeOffset? GradedAt);

/// <summary>One student of the assignment's classrooms in the teacher's review list, handed in or not.</summary>
public sealed record SubmissionRowDto(
    Guid StudentId,
    string StudentName,
    string Nisn,
    string ClassroomName,
    Guid? SubmissionId,
    DateTimeOffset? SubmittedAt,
    StudentAssignmentStatus Status,
    decimal? Score);

/// <summary>A classroom and subject the teacher teaches (from the timetable): what they may give assignments for.</summary>
public sealed record TeachingOptionDto(Guid ClassroomId, string ClassroomName, Guid SubjectId, string SubjectName);

public sealed record DownloadUrlDto(string Url, DateTimeOffset ExpiresAt);
