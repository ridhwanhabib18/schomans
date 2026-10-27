using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.AcademicYears.ListAcademicYears;

public sealed class ListAcademicYearsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<AcademicYearDto>>> Handle(ListAcademicYearsQuery query, CancellationToken ct)
    {
        if (!AcademicYearMapping.CanRead(currentUser.Role))
        {
            return Error.Forbidden("academic_years.forbidden", "You are not allowed to see academic years.");
        }

        // Newest first. The ids are paged in SQL, the terms and counts are loaded for the page in two more queries.
        var ids = db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ThenBy(y => y.Id).Select(y => y.Id);
        var page = await ids.ToPagedResultAsync(query.Page, query.PageSize, ct);
        var years = await AcademicYearMapping.LoadAsync(db, page.Items, ct);
        var order = page.Items.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return new PagedResult<AcademicYearDto>(years.OrderBy(y => order[y.Id]).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
