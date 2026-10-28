using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Subjects.DeleteSubject;

public sealed class DeleteSubjectHandler(IAppDbContext db)
{
    public async Task<Result<Unit>> Handle(DeleteSubjectCommand command, CancellationToken ct)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (subject is null)
        {
            return Error.NotFound("subjects.not_found", "The subject does not exist.");
        }

        if (await db.Schedules.AnyAsync(s => s.SubjectId == subject.Id, ct) || await db.Assignments.AnyAsync(a => a.SubjectId == subject.Id, ct))
        {
            return Error.Conflict("subjects.in_use", "This subject is in the timetable or has assignments, so it cannot be deleted.");
        }

        db.Subjects.Remove(subject);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
