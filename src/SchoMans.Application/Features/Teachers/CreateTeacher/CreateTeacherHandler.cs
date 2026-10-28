using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Teachers.CreateTeacher;

public sealed class CreateTeacherHandler(IAppDbContext db)
{
    public async Task<Result<TeacherDto>> Handle(CreateTeacherCommand command, CancellationToken ct)
    {
        var email = User.NormalizeEmail(command.Email);
        var nip = command.Nip.Trim();

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return Error.Conflict("users.email_taken", "Another account already uses this email.");
        }

        if (await db.Teachers.AnyAsync(t => t.Nip == nip, ct))
        {
            return Error.Conflict("teachers.nip_taken", "Another teacher already has this NIP.");
        }

        var user = User.Create(email, command.FullName, UserRole.Teacher);
        var teacher = Teacher.Create(user.Id, nip);
        db.Users.Add(user);
        db.Teachers.Add(teacher);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("teachers.duplicate", "A teacher with this NIP or email was just created.");
        }

        var row = await db.Teachers.AsNoTracking().Where(t => t.Id == teacher.Id).Select(TeacherMapping.Projection).FirstAsync(ct);
        return TeacherMapping.ToDto(row, isHomeroomTeacher: false);
    }
}
