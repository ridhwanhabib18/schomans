namespace SchoMans.Application.Features.Assignments.CreateAttachmentUploadUrl;

/// <summary>Step 1 of attaching a file: a presigned URL for the teacher's browser. Save the returned key with the assignment.</summary>
public sealed record CreateAttachmentUploadUrlCommand(string ContentType, long SizeBytes);
