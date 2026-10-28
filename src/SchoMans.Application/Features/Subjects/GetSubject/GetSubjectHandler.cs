using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Subjects.GetSubject;

public sealed class GetSubjectHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<SubjectDto>> Handle(GetSubjectQuery query, CancellationToken ct)
    {
        if (!SubjectMapping.CanRead(currentUser.Role))
        {
            return Error.Forbidden("subjects.forbidden", "You are not allowed to see subjects.");
        }

        var row = await db.Subjects.AsNoTracking().Where(s => s.Id == query.Id).Select(SubjectMapping.Projection).FirstOrDefaultAsync(ct);
        return row is null ? Error.NotFound("subjects.not_found", "The subject does not exist.") : SubjectMapping.ToDto(row);
    }
}
