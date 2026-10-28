using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Students.CreateStudent;

public sealed class CreateStudentHandler(IAppDbContext db)
{
    public async Task<Result<StudentDto>> Handle(CreateStudentCommand command, CancellationToken ct)
    {
        if (!await db.Classrooms.AnyAsync(c => c.Id == command.ClassroomId, ct))
        {
            return Error.NotFound("classrooms.not_found", "The classroom does not exist.");
        }

        var email = User.NormalizeEmail(command.Email);
        var nisn = command.Nisn.Trim();

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return Error.Conflict("users.email_taken", "Another account already uses this email.");
        }

        if (await db.Students.AnyAsync(s => s.Nisn == nisn, ct))
        {
            return Error.Conflict("students.nisn_taken", "Another student already has this NISN.");
        }

        // The account has no Keycloak id yet: the first login links it by email (see SyncUserHandler).
        var user = User.Create(email, command.FullName, UserRole.Student);
        var student = Student.Create(user.Id, nisn, command.ClassroomId, command.DateOfBirth);
        db.Users.Add(user);
        db.Students.Add(student);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Somebody saved the same email or NISN between the checks above and the insert.
            return Error.Conflict("students.duplicate", "A student with this NISN or email was just created.");
        }

        var row = await db.Students.AsNoTracking().Where(s => s.Id == student.Id).Select(StudentMapping.Projection).FirstAsync(ct);
        return StudentMapping.ToDto(row);
    }
}
