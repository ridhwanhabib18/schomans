using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Teachers.DeleteTeacher;

public sealed class DeleteTeacherHandler(IAppDbContext db)
{
    public async Task<Result<Unit>> Handle(DeleteTeacherCommand command, CancellationToken ct)
    {
        var teacher = await db.Teachers.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == command.Id, ct);
        if (teacher is null)
        {
            return Error.NotFound("teachers.not_found", "The teacher does not exist.");
        }

        // A classroom always has a homeroom teacher, so appoint somebody else first.
        if (await db.Classrooms.AnyAsync(c => c.HomeroomTeacherId == teacher.Id, ct))
        {
            return Error.Conflict("teachers.is_homeroom", "This teacher is the homeroom teacher of a classroom. Appoint another teacher first.");
        }

        // Lessons and assignments name their teacher; give the lessons to somebody else first.
        if (await db.Schedules.AnyAsync(s => s.TeacherId == teacher.Id, ct) || await db.Assignments.AnyAsync(a => a.TeacherId == teacher.Id, ct))
        {
            return Error.Conflict("teachers.has_lessons", "This teacher has lessons in the timetable or assignments. Give the lessons to another teacher first.");
        }

        db.Teachers.Remove(teacher);
        if (teacher.User is not null)
        {
            db.Users.Remove(teacher.User);
        }

        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
