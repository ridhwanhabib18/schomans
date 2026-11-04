namespace SchoMans.Application.Features.TeacherNotes;

/// <param name="IsMine">True when the signed-in teacher wrote the note, so the screen offers the delete button.</param>
public sealed record TeacherNoteDto(Guid Id, Guid StudentId, string AuthorName, DateOnly Date, string Body, bool IsMine);
