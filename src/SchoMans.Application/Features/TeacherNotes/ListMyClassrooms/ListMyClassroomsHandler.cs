using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.TeacherNotes.ListMyClassrooms;

public sealed record ListMyClassroomsQuery;

/// <param name="IsHomeroom">True when the teacher is the homeroom teacher: only then do the progress summaries of the classroom open.</param>
public sealed record MyClassroomDto(Guid Id, string Name, bool IsHomeroom);

/// <summary>The classrooms of the current academic year the signed-in teacher works in (homeroom or timetable), for the pickers of the notes and summaries screens.</summary>
public sealed class ListMyClassroomsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<List<MyClassroomDto>>> Handle(ListMyClassroomsQuery query, CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null)
        {
            return new List<MyClassroomDto>();
        }

        var classroomIds = SchoolMembership.TeacherClassroomIds(db, teacherId.Value);
        return await db.Classrooms.AsNoTracking()
            .Where(c => c.AcademicYear!.IsActive && classroomIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new MyClassroomDto(c.Id, c.Name, c.HomeroomTeacherId == teacherId.Value))
            .ToListAsync(ct);
    }
}
