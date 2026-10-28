using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Assignments.DeleteAssignment;

/// <summary>The teacher removes an assignment. Scored work stays as history (soft delete), so it cannot vanish from grades later.</summary>
public sealed class DeleteAssignmentHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Unit>> Handle(DeleteAssignmentCommand command, CancellationToken ct)
    {
        var assignment = await db.Assignments.FirstOrDefaultAsync(a => a.Id == command.Id, ct);
        if (assignment is null)
        {
            return Error.NotFound("assignments.not_found", "The assignment does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != assignment.TeacherId)
        {
            return Error.Forbidden("assignments.not_yours", "Only the teacher who gave the assignment can delete it.");
        }

        if (await db.Submissions.AnyAsync(s => s.AssignmentId == assignment.Id && s.GradedAt != null, ct))
        {
            return Error.Conflict("assignments.has_scores", "Some work is already scored, so the assignment cannot be deleted.");
        }

        db.Assignments.Remove(assignment);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
