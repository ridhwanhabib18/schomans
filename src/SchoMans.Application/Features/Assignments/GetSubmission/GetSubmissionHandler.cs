using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Assignments.GetSubmission;

public sealed record GetSubmissionQuery(Guid Id);

public sealed class GetSubmissionHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<SubmissionDto>> Handle(GetSubmissionQuery query, CancellationToken ct)
    {
        var owner = await db.Submissions.AsNoTracking()
            .Where(s => s.Id == query.Id)
            .Select(s => new { s.StudentId, s.Assignment!.TeacherId })
            .FirstOrDefaultAsync(ct);
        if (owner is null)
        {
            return Error.NotFound("submissions.not_found", "The handed-in work does not exist.");
        }

        if (!await SubmissionAccess.CanViewAsync(db, currentUser, owner.StudentId, owner.TeacherId, ct))
        {
            return Error.Forbidden("submissions.forbidden", "You cannot open this work.");
        }

        return await db.Submissions.AsNoTracking().Where(s => s.Id == query.Id).Select(AssignmentLoader.Projection).FirstAsync(ct);
    }
}
