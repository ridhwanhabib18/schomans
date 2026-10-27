using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Students.GetStudent;

public sealed class GetStudentHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<StudentDto>> Handle(GetStudentQuery query, CancellationToken ct)
    {
        if (!StudentAccess.CanRead(currentUser.Role))
        {
            return Error.Forbidden("students.forbidden", "You are not allowed to see students.");
        }

        // A teacher asking for a student outside their classrooms gets "not found", never a hint that the student exists.
        var row = await StudentAccess.Scope(db.Students.AsNoTracking(), currentUser)
            .Where(s => s.Id == query.Id)
            .Select(StudentMapping.Projection)
            .FirstOrDefaultAsync(ct);

        return row is null
            ? Error.NotFound("students.not_found", "The student does not exist.")
            : StudentMapping.ToDto(row);
    }
}
