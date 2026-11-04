using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Assignments.GetSubmission;

public sealed record GetSubmissionQuery(Guid Id);

public sealed class GetSubmissionHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<SubmissionDto>> Handle(GetSubmissionQuery query, CancellationToken ct)
    {
        var owner = await db.Submissions.AsNoTracking()
            .Where(s => s.Id == query.Id)
            .Select(s => new { s.StudentId, s.Assignment!.TeacherId, s.Assignment.Type, s.Assignment.DueAt })
            .FirstOrDefaultAsync(ct);
        if (owner is null)
        {
            return Error.NotFound("submissions.not_found", "The handed-in work does not exist.");
        }

        if (!await SubmissionAccess.CanViewAsync(db, currentUser, owner.StudentId, owner.TeacherId, ct))
        {
            return Error.Forbidden("submissions.forbidden", "You cannot open this work.");
        }

        var submission = await db.Submissions.AsNoTracking().Where(s => s.Id == query.Id).Select(AssignmentLoader.Projection).FirstAsync(ct);
        var isStaff = currentUser.Role is UserRole.Teacher or UserRole.Principal;
        return !isStaff && owner.Type == AssignmentType.Quiz && clock.UtcNow < owner.DueAt ? QuizRedaction.Hide(submission) : submission;
    }
}
