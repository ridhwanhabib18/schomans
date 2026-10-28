namespace SchoMans.Application.Features.Teachers.UpdateTeacher;

public sealed record UpdateTeacherCommand(Guid Id, string FullName, string Email, string Nip, string RowVersion);
