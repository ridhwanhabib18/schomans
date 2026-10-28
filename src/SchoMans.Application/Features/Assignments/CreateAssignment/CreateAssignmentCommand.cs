namespace SchoMans.Application.Features.Assignments.CreateAssignment;

/// <param name="Key">The key returned by the attachment upload URL, after the file was uploaded.</param>
/// <param name="FileName">The original name of the file, shown to the students.</param>
public sealed record NewAttachment(string Key, string FileName);

/// <summary>The teacher gives an assignment to one or more classrooms they teach the subject in. The students are notified.</summary>
public sealed record CreateAssignmentCommand(
    string Title,
    string? Description,
    DateTimeOffset DueAt,
    Guid SubjectId,
    IReadOnlyList<Guid> ClassroomIds,
    IReadOnlyList<NewAttachment>? Attachments);
