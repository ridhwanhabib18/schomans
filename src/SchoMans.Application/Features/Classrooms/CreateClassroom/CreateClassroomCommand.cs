namespace SchoMans.Application.Features.Classrooms.CreateClassroom;

public sealed record CreateClassroomCommand(string Name, Guid AcademicYearId, Guid HomeroomTeacherId);
