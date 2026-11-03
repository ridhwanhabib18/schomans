namespace SchoMans.Application.Features.TeacherNotes.CreateTeacherNote;

/// <param name="Date">The school day the note is about; today when left out. It decides which month's summary uses the note.</param>
public sealed record CreateTeacherNoteCommand(Guid StudentId, string Body, DateOnly? Date = null);
