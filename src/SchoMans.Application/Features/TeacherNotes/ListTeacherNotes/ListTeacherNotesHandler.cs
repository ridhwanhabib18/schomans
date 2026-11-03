using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.TeacherNotes.ListTeacherNotes;

/// <summary>The notes about a student, newest first, for the teachers who work in the student's classroom. Parents and students never see them.</summary>
public sealed class ListTeacherNotesHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<TeacherNoteDto>>> Handle(ListTeacherNotesQuery query, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == query.StudentId, ct))
        {
            return Error.NotFound("notes.student_not_found", "The student does not exist.");
        }

        if (!await SchoolMembership.CanSeeStudentAsync(db, currentUser, query.StudentId, ct))
        {
            return Error.Forbidden("notes.not_your_student", "You only read notes about students in your own classrooms.");
        }

        var me = currentUser.UserId;
        var page = await db.TeacherNotes.AsNoTracking()
            .Where(n => n.StudentId == query.StudentId)
            .OrderByDescending(n => n.Date).ThenByDescending(n => n.CreatedAt)
            .Select(n => new TeacherNoteDto(n.Id, n.StudentId, n.Author!.FullName, n.Date, n.Body, n.AuthorId == me))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
        return page;
    }
}
