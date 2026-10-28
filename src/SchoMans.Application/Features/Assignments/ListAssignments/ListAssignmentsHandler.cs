using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Assignments.ListAssignments;

public sealed class ListAssignmentsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<AssignmentListItemDto>>> Handle(ListAssignmentsQuery query, CancellationToken ct)
    {
        var assignments = db.Assignments.AsNoTracking();
        if (currentUser.Role == UserRole.Teacher)
        {
            var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
            assignments = assignments.Where(a => a.TeacherId == teacherId);
        }
        else if (currentUser.Role != UserRole.Principal)
        {
            return Error.Forbidden("assignments.forbidden", "You are not allowed to see this list.");
        }

        if (query.ClassroomId is { } classroomId)
        {
            assignments = assignments.Where(a => a.Classrooms.Any(c => c.ClassroomId == classroomId));
        }

        if (query.SubjectId is { } subjectId)
        {
            assignments = assignments.Where(a => a.SubjectId == subjectId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            assignments = assignments.Where(a => a.Title.Contains(term));
        }

        var page = await assignments
            .OrderByDescending(a => a.DueAt)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Title,
                SubjectName = a.Subject!.Name,
                ClassroomIds = a.Classrooms.Select(c => c.ClassroomId).ToList(),
                a.DueAt,
                StudentCount = db.Students.Count(s => s.Status == StudentStatus.Active && a.Classrooms.Any(c => c.ClassroomId == s.ClassroomId)),
                SubmittedCount = db.Submissions.Count(s => s.AssignmentId == a.Id),
                GradedCount = db.Submissions.Count(s => s.AssignmentId == a.Id && s.GradedAt != null),
            })
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        var classroomIds = page.Items.SelectMany(i => i.ClassroomIds).Distinct().ToList();
        var names = await db.Classrooms.AsNoTracking().Where(c => classroomIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var items = page.Items
            .Select(i => new AssignmentListItemDto(
                i.Id,
                i.Title,
                i.SubjectName,
                i.ClassroomIds.Select(id => names.GetValueOrDefault(id, "?")).Order(StringComparer.CurrentCultureIgnoreCase).ToList(),
                i.DueAt,
                i.StudentCount,
                i.SubmittedCount,
                i.GradedCount))
            .ToList();
        return new PagedResult<AssignmentListItemDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
