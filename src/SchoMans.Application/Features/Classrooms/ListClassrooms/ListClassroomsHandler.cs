using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Classrooms.ListClassrooms;

public sealed class ListClassroomsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<ClassroomDto>>> Handle(ListClassroomsQuery query, CancellationToken ct)
    {
        if (!ClassroomAccess.CanRead(currentUser.Role))
        {
            return Error.Forbidden("classrooms.forbidden", "You are not allowed to see classrooms.");
        }

        var classrooms = db.Classrooms.AsNoTracking();

        if (query.AcademicYearId is { } yearId)
        {
            classrooms = classrooms.Where(c => c.AcademicYearId == yearId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            classrooms = classrooms.Where(c => c.Name.Contains(term));
        }

        var ordered = (query.Sort?.ToLowerInvariant(), query.Desc) switch
        {
            ("academicyear", false) => classrooms.OrderBy(c => c.AcademicYear!.StartDate).ThenBy(c => c.Name),
            ("academicyear", true) => classrooms.OrderByDescending(c => c.AcademicYear!.StartDate).ThenBy(c => c.Name),
            (_, true) => classrooms.OrderByDescending(c => c.Name),
            _ => classrooms.OrderBy(c => c.Name),
        };

        var page = await ordered.ThenBy(c => c.Id).Select(ClassroomMapping.Projection).ToPagedResultAsync(query.Page, query.PageSize, ct);
        var counts = await ClassroomMapping.StudentCounts(db, page.Items.Select(c => c.Id).ToList(), ct);
        return new PagedResult<ClassroomDto>(
            page.Items.Select(c => ClassroomMapping.ToDto(c, counts.GetValueOrDefault(c.Id))).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}
