namespace SchoMans.Application.Features.Assignments.SubmitAssignment;

/// <summary>Hands in work: a file uploaded beforehand (its key and original name), a text, or both. Replaces earlier work until it is scored.</summary>
public sealed record SubmitAssignmentCommand(Guid AssignmentId, string? FileKey, string? FileName, string? TextBody);
