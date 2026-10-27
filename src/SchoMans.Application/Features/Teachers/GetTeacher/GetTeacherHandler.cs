using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Teachers.GetTeacher;

public sealed class GetTeacherHandler(IAppDbContext db)
{
    public async Task<Result<TeacherDto>> Handle(GetTeacherQuery query, CancellationToken ct)
    {
        var row = await db.Teachers.AsNoTracking().Where(t => t.Id == query.Id).Select(TeacherMapping.Projection).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return Error.NotFound("teachers.not_found", "The teacher does not exist.");
        }

        var homeroom = await TeacherMapping.HomeroomTeacherIds(db, [row.Id], ct);
        return TeacherMapping.ToDto(row, homeroom.Contains(row.Id));
    }
}
