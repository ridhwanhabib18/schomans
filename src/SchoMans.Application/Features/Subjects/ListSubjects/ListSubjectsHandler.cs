using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Subjects.ListSubjects;

public sealed class ListSubjectsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<SubjectDto>>> Handle(ListSubjectsQuery query, CancellationToken ct)
    {
        if (!SubjectMapping.CanRead(currentUser.Role))
        {
            return Error.Forbidden("subjects.forbidden", "You are not allowed to see subjects.");
        }

        var subjects = db.Subjects.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            subjects = subjects.Where(s => s.Name.Contains(term));
        }

        var ordered = (query.Sort?.ToLowerInvariant(), query.Desc) switch
        {
            ("minimumpassingscore", false) => subjects.OrderBy(s => s.MinimumPassingScore).ThenBy(s => s.Name),
            ("minimumpassingscore", true) => subjects.OrderByDescending(s => s.MinimumPassingScore).ThenBy(s => s.Name),
            (_, true) => subjects.OrderByDescending(s => s.Name),
            _ => subjects.OrderBy(s => s.Name),
        };

        var page = await ordered.ThenBy(s => s.Id).Select(SubjectMapping.Projection).ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<SubjectDto>(page.Items.Select(SubjectMapping.ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
