using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Students.ListStudents;

public sealed class ListStudentsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<StudentDto>>> Handle(ListStudentsQuery query, CancellationToken ct)
    {
        if (!StudentAccess.CanRead(currentUser.Role))
        {
            return Error.Forbidden("students.forbidden", "You are not allowed to see students.");
        }

        var students = StudentAccess.Scope(db.Students.AsNoTracking(), currentUser);

        if (query.ClassroomId is { } classroomId)
        {
            students = students.Where(s => s.ClassroomId == classroomId);
        }

        if (query.Status is { } status)
        {
            students = students.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            students = students.Where(s => s.Nisn.Contains(term) || s.User!.FullName.Contains(term) || s.User.Email.Contains(term));
        }

        var rows = Sort(students, query.Sort, query.Desc).Select(StudentMapping.Projection);
        var page = await rows.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<StudentDto>(page.Items.Select(StudentMapping.ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    // The name is the tie-breaker so paging stays stable when many rows share the sorted value.
    private static IQueryable<Domain.Students.Student> Sort(IQueryable<Domain.Students.Student> students, string? sort, bool desc)
    {
        var ordered = (sort?.ToLowerInvariant(), desc) switch
        {
            ("nisn", false) => students.OrderBy(s => s.Nisn),
            ("nisn", true) => students.OrderByDescending(s => s.Nisn),
            ("classroom", false) => students.OrderBy(s => s.Classroom!.Name),
            ("classroom", true) => students.OrderByDescending(s => s.Classroom!.Name),
            ("dateofbirth", false) => students.OrderBy(s => s.DateOfBirth),
            ("dateofbirth", true) => students.OrderByDescending(s => s.DateOfBirth),
            (_, true) => students.OrderByDescending(s => s.User!.FullName),
            _ => students.OrderBy(s => s.User!.FullName),
        };

        return ordered.ThenBy(s => s.User!.FullName).ThenBy(s => s.Id);
    }
}
