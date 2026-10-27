namespace SchoMans.Application.Features.Students.CreateStudent;

public sealed record CreateStudentCommand(string FullName, string Email, string Nisn, Guid ClassroomId, DateOnly DateOfBirth);
