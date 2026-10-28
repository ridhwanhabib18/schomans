using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.AcademicYears.GetAcademicYear;

public sealed class GetAcademicYearHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<AcademicYearDto>> Handle(GetAcademicYearQuery query, CancellationToken ct)
    {
        if (!AcademicYearMapping.CanRead(currentUser.Role))
        {
            return Error.Forbidden("academic_years.forbidden", "You are not allowed to see academic years.");
        }

        var years = await AcademicYearMapping.LoadAsync(db, [query.Id], ct);
        return years.Count == 0
            ? Error.NotFound("academic_years.not_found", "The academic year does not exist.")
            : years[0];
    }
}
