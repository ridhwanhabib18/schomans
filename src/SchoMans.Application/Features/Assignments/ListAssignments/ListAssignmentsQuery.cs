namespace SchoMans.Application.Features.Assignments.ListAssignments;

/// <summary>The assignments a teacher gave (all of them for the principal), newest due date first.</summary>
public sealed record ListAssignmentsQuery(int Page = 1, int PageSize = 20, Guid? ClassroomId = null, Guid? SubjectId = null, string? Search = null);
