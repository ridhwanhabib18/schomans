using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Teachers.UpdateTeacher;

public sealed class UpdateTeacherHandler(IAppDbContext db)
{
    public async Task<Result<TeacherDto>> Handle(UpdateTeacherCommand command, CancellationToken ct)
    {
        var teacher = await db.Teachers.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == command.Id, ct);
        if (teacher?.User is null)
        {
            return Error.NotFound("teachers.not_found", "The teacher does not exist.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        var email = User.NormalizeEmail(command.Email);
        var nip = command.Nip.Trim();

        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != teacher.UserId, ct))
        {
            return Error.Conflict("users.email_taken", "Another account already uses this email.");
        }

        if (await db.Teachers.AnyAsync(t => t.Nip == nip && t.Id != teacher.Id, ct))
        {
            return Error.Conflict("teachers.nip_taken", "Another teacher already has this NIP.");
        }

        var identityChanged = teacher.User.Email != email || teacher.User.FullName != command.FullName.Trim();
        if (identityChanged && !teacher.User.TryUpdateIdentity(email, command.FullName))
        {
            return Error.Conflict(
                "users.managed_by_keycloak",
                "This teacher has already signed in, so the name and email are managed by their account and cannot be changed here.");
        }

        teacher.UpdateNip(nip);
        db.SetExpectedRowVersion(teacher, rowVersion);

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
            return Error.Conflict("teachers.duplicate", "A teacher with this NIP or email already exists.");
        }

        var row = await db.Teachers.AsNoTracking().Where(t => t.Id == teacher.Id).Select(TeacherMapping.Projection).FirstAsync(ct);
        var homeroom = await TeacherMapping.HomeroomTeacherIds(db, [row.Id], ct);
        return TeacherMapping.ToDto(row, homeroom.Contains(row.Id));
    }
}
