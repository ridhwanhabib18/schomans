namespace SchoMans.Application.Features.Students.UpdateStudent;

/// <param name="RowVersion">The row version the client got with the student; a stale one is rejected with 409.</param>
public sealed record UpdateStudentCommand(
    Guid Id,
    string FullName,
    string Email,
    string Nisn,
    Guid ClassroomId,
    DateOnly DateOfBirth,
    string RowVersion);
