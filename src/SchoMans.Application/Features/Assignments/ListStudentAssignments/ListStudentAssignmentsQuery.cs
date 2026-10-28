namespace SchoMans.Application.Features.Assignments.ListStudentAssignments;

/// <param name="StudentId">For a parent: the child. Null for the signed-in student.</param>
public sealed record ListStudentAssignmentsQuery(int Page = 1, int PageSize = 20, Guid? StudentId = null);
