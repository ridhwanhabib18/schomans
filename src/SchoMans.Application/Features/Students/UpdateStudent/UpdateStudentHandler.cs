using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Students.UpdateStudent;

public sealed class UpdateStudentHandler(IAppDbContext db)
{
    public async Task<Result<StudentDto>> Handle(UpdateStudentCommand command, CancellationToken ct)
    {
        var student = await db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (student?.User is null)
        {
            return Error.NotFound("students.not_found", "The student does not exist.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        var email = User.NormalizeEmail(command.Email);
        var nisn = command.Nisn.Trim();

        if (!await db.Classrooms.AnyAsync(c => c.Id == command.ClassroomId, ct))
        {
            return Error.NotFound("classrooms.not_found", "The classroom does not exist.");
        }

        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != student.UserId, ct))
        {
            return Error.Conflict("users.email_taken", "Another account already uses this email.");
        }

        if (await db.Students.AnyAsync(s => s.Nisn == nisn && s.Id != student.Id, ct))
        {
            return Error.Conflict("students.nisn_taken", "Another student already has this NISN.");
        }

        var identityChanged = student.User.Email != email || student.User.FullName != command.FullName.Trim();
        if (identityChanged && !student.User.TryUpdateIdentity(email, command.FullName))
        {
            return Error.Conflict(
                "users.managed_by_keycloak",
                "This student has already signed in, so the name and email are managed by their account and cannot be changed here.");
        }

        if (student.ClassroomId != command.ClassroomId && !student.MoveTo(command.ClassroomId))
        {
            return Error.Conflict("students.graduated", "A graduated student cannot be moved to a classroom.");
        }

        student.UpdateDetails(nisn, command.DateOfBirth);
        db.SetExpectedRowVersion(student, rowVersion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommonErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("students.duplicate", "A student with this NISN or email already exists.");
        }

        var row = await db.Students.AsNoTracking().Where(s => s.Id == student.Id).Select(StudentMapping.Projection).FirstAsync(ct);
        return StudentMapping.ToDto(row);
    }
}
