namespace SchoMans.Application.Features.TeacherNotes.ListTeacherNotes;

public sealed record ListTeacherNotesQuery(Guid StudentId, int Page = 1, int PageSize = 20);
