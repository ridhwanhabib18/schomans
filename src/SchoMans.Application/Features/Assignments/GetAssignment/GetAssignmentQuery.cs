namespace SchoMans.Application.Features.Assignments.GetAssignment;

/// <param name="StudentId">For a parent: which child's work to include.</param>
public sealed record GetAssignmentQuery(Guid Id, Guid? StudentId = null);
