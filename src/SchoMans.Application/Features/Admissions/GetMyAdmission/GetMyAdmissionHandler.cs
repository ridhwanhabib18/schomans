using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Admissions.GetMyAdmission;

public sealed record GetMyAdmissionQuery;

/// <summary>The applicant's dashboard: their application with its status at this stage, and the period they can apply in.</summary>
public sealed class GetMyAdmissionHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<MyAdmissionDto>> Handle(GetMyAdmissionQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var applicant = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: false, ct);
        if (applicant is not null)
        {
            return new MyAdmissionDto(AdmissionSupport.ToPublic(applicant.AdmissionPeriod!, now), AdmissionSupport.ToMine(applicant, now));
        }

        var open = await AdmissionSupport.OpenPeriodAsync(db, now, ct);
        return new MyAdmissionDto(open is null ? null : AdmissionSupport.ToPublic(open, now), null);
    }
}
