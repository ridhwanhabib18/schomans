using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Teachers.ListTeachers;

public sealed class ListTeachersHandler(IAppDbContext db)
{
    public async Task<Result<PagedResult<TeacherDto>>> Handle(ListTeachersQuery query, CancellationToken ct)
    {
        var teachers = db.Teachers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            teachers = teachers.Where(t => t.Nip.Contains(term) || t.User!.FullName.Contains(term) || t.User.Email.Contains(term));
        }

        var ordered = (query.Sort?.ToLowerInvariant(), query.Desc) switch
        {
            ("nip", false) => teachers.OrderBy(t => t.Nip),
            ("nip", true) => teachers.OrderByDescending(t => t.Nip),
            (_, true) => teachers.OrderByDescending(t => t.User!.FullName),
            _ => teachers.OrderBy(t => t.User!.FullName),
        };

        var page = await ordered.ThenBy(t => t.Id).Select(TeacherMapping.Projection).ToPagedResultAsync(query.Page, query.PageSize, ct);
        var homeroom = await TeacherMapping.HomeroomTeacherIds(db, page.Items.Select(t => t.Id).ToList(), ct);
        return new PagedResult<TeacherDto>(
            page.Items.Select(t => TeacherMapping.ToDto(t, homeroom.Contains(t.Id))).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
