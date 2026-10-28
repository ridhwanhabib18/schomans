using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Assignments.ListSubmissions;

public sealed record ListSubmissionsQuery(Guid AssignmentId);

/// <summary>
/// The review list of an assignment: every student of its classrooms, handed in or not. Students who left the classroom
/// but handed in work are kept, so no work disappears from the teacher's list.
/// </summary>
public sealed class ListSubmissionsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<SubmissionRowDto>>> Handle(ListSubmissionsQuery query, CancellationToken ct)
    {
        var assignment = await db.Assignments.AsNoTracking()
            .Where(a => a.Id == query.AssignmentId)
            .Select(a => new { a.Id, a.TeacherId, ClassroomIds = a.Classrooms.Select(c => c.ClassroomId).ToList() })
            .FirstOrDefaultAsync(ct);
        if (assignment is null)
        {
            return Error.NotFound("assignments.not_found", "The assignment does not exist.");
        }

        var viewer = await AssignmentAccess.ForAsync(db, currentUser, assignment.Id, assignment.TeacherId, null, ct);
        if (!viewer.CanView || currentUser.Role is not (UserRole.Teacher or UserRole.Principal))
        {
            return Error.Forbidden("assignments.not_yours", "Only the teacher who gave the assignment sees the handed-in work.");
        }

        var submissions = await db.Submissions.AsNoTracking()
            .Where(s => s.AssignmentId == assignment.Id)
            .Select(s => new { s.Id, s.StudentId, s.SubmittedAt, s.IsLate, s.Score })
            .ToListAsync(ct);
        var submittedIds = submissions.Select(s => s.StudentId).ToList();

        var students = await db.Students.AsNoTracking()
            .Where(s => (assignment.ClassroomIds.Contains(s.ClassroomId) && s.Status == StudentStatus.Active) || submittedIds.Contains(s.Id))
            .Select(s => new { s.Id, s.User!.FullName, s.Nisn, ClassroomName = s.Classroom!.Name })
            .ToListAsync(ct);

        var byStudent = submissions.ToDictionary(s => s.StudentId);
        return students
            .OrderBy(s => s.ClassroomName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(s => s.FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(s =>
            {
                var submission = byStudent.GetValueOrDefault(s.Id);
                var status = submission is null
                    ? StudentAssignmentStatus.NotSubmitted
                    : submission.IsLate ? StudentAssignmentStatus.Late : StudentAssignmentStatus.Submitted;
                return new SubmissionRowDto(s.Id, s.FullName, s.Nisn, s.ClassroomName, submission?.Id, submission?.SubmittedAt, status, submission?.Score);
            })
            .ToList();
    }
}
