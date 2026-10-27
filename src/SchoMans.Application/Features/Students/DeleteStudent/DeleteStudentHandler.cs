using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Students.DeleteStudent;

/// <summary>
/// Soft-deletes the student, their account row, and the links to their parents. A later login with the same Keycloak
/// account is refused (see SyncUserHandler); disabling the Keycloak account itself arrives with account provisioning.
/// </summary>
public sealed class DeleteStudentHandler(IAppDbContext db)
{
    public async Task<Result<Unit>> Handle(DeleteStudentCommand command, CancellationToken ct)
    {
        var student = await db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (student is null)
        {
            return Error.NotFound("students.not_found", "The student does not exist.");
        }

        var parentLinks = await db.StudentParents.Where(p => p.StudentId == student.Id).ToListAsync(ct);
        db.StudentParents.RemoveRange(parentLinks);
        db.Students.Remove(student);
        if (student.User is not null)
        {
            db.Users.Remove(student.User);
        }

        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
