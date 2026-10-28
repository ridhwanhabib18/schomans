using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Assignments.GetAssignment;

/// <summary>An assignment with its attachments; for a student (or a parent) also the student's own work.</summary>
public sealed class GetAssignmentHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<AssignmentDto>> Handle(GetAssignmentQuery query, CancellationToken ct)
    {
        var teacherId = await db.Assignments.AsNoTracking().Where(a => a.Id == query.Id).Select(a => (Guid?)a.TeacherId).FirstOrDefaultAsync(ct);
        if (teacherId is null)
        {
            return Error.NotFound("assignments.not_found", "The assignment does not exist.");
        }

        var viewer = await AssignmentAccess.ForAsync(db, currentUser, query.Id, teacherId.Value, query.StudentId, ct);
        if (!viewer.CanView)
        {
            return Error.Forbidden("assignments.not_yours", "This assignment is not for you.");
        }

        return (await AssignmentLoader.LoadAsync(db, query.Id, viewer.IsOwner, viewer.StudentId, ct))!;
    }
}
