using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Classrooms.UpdateClassroom;

public sealed class UpdateClassroomHandler(IAppDbContext db)
{
    public async Task<Result<ClassroomDto>> Handle(UpdateClassroomCommand command, CancellationToken ct)
    {
        var classroom = await db.Classrooms.FirstOrDefaultAsync(c => c.Id == command.Id, ct);
        if (classroom is null)
        {
            return Error.NotFound("classrooms.not_found", "The classroom does not exist.");
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return CommonErrors.InvalidRowVersion;
        }

        if (!await db.Teachers.AnyAsync(t => t.Id == command.HomeroomTeacherId, ct))
        {
            return Error.NotFound("teachers.not_found", "The homeroom teacher does not exist.");
        }

        var name = command.Name.Trim();
        if (await db.Classrooms.AnyAsync(c => c.AcademicYearId == classroom.AcademicYearId && c.Name == name && c.Id != classroom.Id, ct))
        {
            return Error.Conflict("classrooms.name_taken", "This academic year already has a classroom with this name.");
        }

        classroom.Update(name, command.HomeroomTeacherId);
        db.SetExpectedRowVersion(classroom, rowVersion);

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
            return Error.Conflict("classrooms.name_taken", "This academic year already has a classroom with this name.");
        }

        var row = await db.Classrooms.AsNoTracking().Where(c => c.Id == classroom.Id).Select(ClassroomMapping.Projection).FirstAsync(ct);
        var counts = await ClassroomMapping.StudentCounts(db, [row.Id], ct);
        return ClassroomMapping.ToDto(row, counts.GetValueOrDefault(row.Id));
    }
}
