using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Assignments.ListStudentAssignments;

/// <summary>
/// A student's assignments (or a parent's child's), nearest due date first: the open ones by due date, then the ones
/// whose due date has passed, most recent first. Each with its status: not submitted, submitted, or submitted late.
/// </summary>
public sealed class ListStudentAssignmentsHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<PagedResult<StudentAssignmentDto>>> Handle(ListStudentAssignmentsQuery query, CancellationToken ct)
    {
        var studentId = await ResolveStudentAsync(query.StudentId, ct);
        if (studentId.IsFailure)
        {
            return studentId.Error;
        }

        var id = studentId.Value;
        var classroomId = await db.Students.AsNoTracking().Where(s => s.Id == id).Select(s => s.ClassroomId).FirstAsync(ct);
        var now = clock.UtcNow;

        var page = await db.Assignments.AsNoTracking()
            .Where(a => a.Classrooms.Any(c => c.ClassroomId == classroomId))
            .OrderBy(a => a.DueAt < now ? 1 : 0)
            .ThenBy(a => a.DueAt < now ? null : (DateTimeOffset?)a.DueAt)
            .ThenByDescending(a => a.DueAt)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Title,
                SubjectName = a.Subject!.Name,
                TeacherName = a.Teacher!.User!.FullName,
                a.DueAt,
                Submission = db.Submissions
                    .Where(s => s.AssignmentId == a.Id && s.StudentId == id)
                    .Select(s => new { s.SubmittedAt, s.IsLate, s.Score })
                    .FirstOrDefault(),
            })
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        var items = page.Items
            .Select(a => new StudentAssignmentDto(
                a.Id,
                a.Title,
                a.SubjectName,
                a.TeacherName,
                a.DueAt,
                a.Submission is null ? StudentAssignmentStatus.NotSubmitted : a.Submission.IsLate ? StudentAssignmentStatus.Late : StudentAssignmentStatus.Submitted,
                a.Submission is null && a.DueAt < now,
                a.Submission?.SubmittedAt,
                a.Submission?.Score))
            .ToList();
        return new PagedResult<StudentAssignmentDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private async Task<Result<Guid>> ResolveStudentAsync(Guid? requested, CancellationToken ct)
    {
        switch (currentUser.Role)
        {
            case UserRole.Student:
                var me = await SchoolMembership.StudentIdAsync(db, currentUser, ct);
                return me is null ? Error.NotFound("students.not_found", "There is no student record for this account.") : me.Value;
            case UserRole.Parent:
                if (requested is null)
                {
                    return Error.Validation("assignments.student_required", "Choose a child.");
                }

                var isChild = await db.StudentParents.AnyAsync(p => p.ParentUserId == currentUser.UserId && p.StudentId == requested, ct);
                return isChild ? requested.Value : Error.Forbidden("assignments.not_your_child", "You can only see your own children's assignments.");
            default:
                return Error.Forbidden("assignments.forbidden", "This list is for students and parents.");
        }
    }
}
