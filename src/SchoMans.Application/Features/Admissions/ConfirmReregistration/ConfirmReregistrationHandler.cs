using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions.ConfirmReregistration;

public sealed record ConfirmReregistrationCommand;

/// <summary>An accepted applicant confirms the place once the results are out and before the re-registration deadline.</summary>
public sealed class ConfirmReregistrationHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<MyApplicationDto>> Handle(ConfirmReregistrationCommand command, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var applicant = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: true, ct);
        if (applicant is null)
        {
            return AdmissionSupport.NoApplication();
        }

        if (applicant.Status != ApplicantStatus.Accepted || !applicant.AdmissionPeriod!.ResultsVisibleAt(now))
        {
            return Error.Conflict("admissions.not_accepted", "Only an accepted applicant confirms the place.");
        }

        if (!applicant.AdmissionPeriod.ReregistrationOpenAt(now))
        {
            return Error.Conflict("admissions.reregistration_closed", "The re-registration deadline has passed.");
        }

        if (!applicant.ConfirmReregistration(now))
        {
            return Error.Conflict("admissions.already_confirmed", "You have already confirmed your place.");
        }

        await db.SaveChangesAsync(ct);
        return AdmissionSupport.ToMine(applicant, now);
    }
}
