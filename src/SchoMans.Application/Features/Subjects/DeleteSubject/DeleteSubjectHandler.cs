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

        // Nothing references a subject yet. Schedules, assignments and grades will add their own checks when they arrive.
        db.Subjects.Remove(subject);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
