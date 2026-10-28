namespace SchoMans.Application.Features.Assignments.CreateSubmissionUploadUrl;

/// <summary>Step 1 of handing in a file: a presigned URL for the student's browser (PDF or image, at most 10 MB).</summary>
public sealed record CreateSubmissionUploadUrlCommand(Guid AssignmentId, string ContentType, long SizeBytes);
