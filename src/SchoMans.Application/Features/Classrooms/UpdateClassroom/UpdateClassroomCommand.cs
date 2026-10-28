namespace SchoMans.Application.Features.Classrooms.UpdateClassroom;

public sealed record UpdateClassroomCommand(Guid Id, string Name, Guid HomeroomTeacherId, string RowVersion);
