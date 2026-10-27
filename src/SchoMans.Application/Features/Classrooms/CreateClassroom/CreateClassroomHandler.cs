using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Classrooms.CreateClassroom;

public sealed class CreateClassroomHandler(IAppDbContext db)
{
    public async Task<Result<ClassroomDto>> Handle(CreateClassroomCommand command, CancellationToken ct)
    {
        if (!await db.AcademicYears.AnyAsync(y => y.Id == command.AcademicYearId, ct))
        {
            return Error.NotFound("academic_years.not_found", "The academic year does not exist.");
        }

        if (!await db.Teachers.AnyAsync(t => t.Id == command.HomeroomTeacherId, ct))
        {
            return Error.NotFound("teachers.not_found", "The homeroom teacher does not exist.");
        }

        var name = command.Name.Trim();
        if (await db.Classrooms.AnyAsync(c => c.AcademicYearId == command.AcademicYearId && c.Name == name, ct))
        {
            return Error.Conflict("classrooms.name_taken", "This academic year already has a classroom with this name.");
        }

        var classroom = Classroom.Create(name, command.AcademicYearId, command.HomeroomTeacherId);
        db.Classrooms.Add(classroom);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("classrooms.name_taken", "This academic year already has a classroom with this name.");
        }

        var row = await db.Classrooms.AsNoTracking().Where(c => c.Id == classroom.Id).Select(ClassroomMapping.Projection).FirstAsync(ct);
        return ClassroomMapping.ToDto(row, studentCount: 0);
    }
}
