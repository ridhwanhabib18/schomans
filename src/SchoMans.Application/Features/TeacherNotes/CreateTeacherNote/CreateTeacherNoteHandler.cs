using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.TeacherNotes.CreateTeacherNote;

/// <summary>A teacher who works in the student's classroom (homeroom or timetable) keeps a short note about the student.</summary>
public sealed class CreateTeacherNoteHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<TeacherNoteDto>> Handle(CreateTeacherNoteCommand command, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == command.StudentId, ct))
        {
            return Error.NotFound("notes.student_not_found", "The student does not exist.");
        }

        if (!await SchoolMembership.CanSeeStudentAsync(db, currentUser, command.StudentId, ct))
        {
            return Error.Forbidden("notes.not_your_student", "You only write notes about students in your own classrooms.");
        }

        var date = command.Date ?? clock.Today;
        if (date > clock.Today)
        {
            return Error.Validation("notes.future_date", "A note cannot be about a day that has not come yet.");
        }

        var note = TeacherNote.Create(command.StudentId, currentUser.UserId, date, command.Body);
        db.TeacherNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return new TeacherNoteDto(note.Id, note.StudentId, currentUser.FullName, note.Date, note.Body, IsMine: true);
    }
}
