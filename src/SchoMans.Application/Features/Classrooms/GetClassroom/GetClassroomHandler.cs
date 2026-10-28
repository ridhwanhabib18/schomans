using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Classrooms.GetClassroom;

public sealed class GetClassroomHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ClassroomDto>> Handle(GetClassroomQuery query, CancellationToken ct)
    {
        if (!ClassroomAccess.CanRead(currentUser.Role))
        {
            return Error.Forbidden("classrooms.forbidden", "You are not allowed to see classrooms.");
        }

        var row = await db.Classrooms.AsNoTracking().Where(c => c.Id == query.Id).Select(ClassroomMapping.Projection).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return Error.NotFound("classrooms.not_found", "The classroom does not exist.");
        }

        var counts = await ClassroomMapping.StudentCounts(db, [row.Id], ct);
        return ClassroomMapping.ToDto(row, counts.GetValueOrDefault(row.Id));
    }
}
