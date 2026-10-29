using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Admissions.GetOpenAdmission;

public sealed record GetOpenAdmissionQuery;

/// <summary>Public: the period that is open now, or else the next one, for the banner of the landing page.</summary>
public sealed class GetOpenAdmissionHandler(IAppDbContext db, ISchoolClock clock)
{
    public async Task<Result<OpenAdmissionDto>> Handle(GetOpenAdmissionQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var period = await AdmissionSupport.OpenPeriodAsync(db, now, ct)
            ?? await db.AdmissionPeriods.AsNoTracking().Where(p => p.OpensAt > now).OrderBy(p => p.OpensAt).FirstOrDefaultAsync(ct);

        return new OpenAdmissionDto(period is null ? null : AdmissionSupport.ToPublic(period, now));
    }
}
