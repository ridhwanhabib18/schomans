namespace SchoMans.Application.Features.Assignments.SubmitAssignment;

/// <summary>Hands in work: a file uploaded beforehand (its key and original name), a text, or both. Replaces earlier work until it is scored.</summary>
/// <param name="KeepFile">Hand in again with a new text but the file of the earlier work (the browser never sees file keys).</param>
public sealed record SubmitAssignmentCommand(Guid AssignmentId, string? FileKey, string? FileName, string? TextBody, bool KeepFile = false);
