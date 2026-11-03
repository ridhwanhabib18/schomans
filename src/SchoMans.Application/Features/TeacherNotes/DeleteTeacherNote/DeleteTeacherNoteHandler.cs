using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.TeacherNotes.DeleteTeacherNote;

/// <summary>Only the teacher who wrote a note may delete it.</summary>
public sealed class DeleteTeacherNoteHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Unit>> Handle(DeleteTeacherNoteCommand command, CancellationToken ct)
    {
        var note = await db.TeacherNotes.FirstOrDefaultAsync(n => n.Id == command.Id, ct);
        if (note is null)
        {
            return Error.NotFound("notes.not_found", "The note does not exist.");
        }

        if (note.AuthorId != currentUser.UserId)
        {
            return Error.Forbidden("notes.not_yours", "You can only delete your own notes.");
        }

        db.TeacherNotes.Remove(note);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
