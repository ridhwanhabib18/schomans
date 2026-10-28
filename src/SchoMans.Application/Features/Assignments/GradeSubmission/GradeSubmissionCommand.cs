namespace SchoMans.Application.Features.Assignments.GradeSubmission;

/// <param name="Score">0 to 100.</param>
public sealed record GradeSubmissionCommand(Guid SubmissionId, decimal Score, string? Feedback);
