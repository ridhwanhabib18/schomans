using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Classrooms.DeleteClassroom;

public sealed class DeleteClassroomHandler(IAppDbContext db)
{
    public async Task<Result<Unit>> Handle(DeleteClassroomCommand command, CancellationToken ct)
    {
        var classroom = await db.Classrooms.FirstOrDefaultAsync(c => c.Id == command.Id, ct);
        if (classroom is null)
        {
            return Error.NotFound("classrooms.not_found", "The classroom does not exist.");
        }

        // Students (also graduated ones) keep a reference to their classroom, so move them out first.
        if (await db.Students.AnyAsync(s => s.ClassroomId == classroom.Id, ct))
        {
            return Error.Conflict("classrooms.has_students", "This classroom still has students. Move them to another classroom first.");
        }

        if (await db.Schedules.AnyAsync(s => s.ClassroomId == classroom.Id, ct))
        {
            return Error.Conflict("classrooms.has_lessons", "This classroom has lessons in the timetable. Remove them first.");
        }

        db.Classrooms.Remove(classroom);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
