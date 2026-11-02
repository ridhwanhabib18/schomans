using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions.StartApplication;

public sealed record StartApplicationCommand;

/// <summary>An applicant starts the application in the period that is open. Starting again returns the same application.</summary>
public sealed class StartApplicationHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<MyApplicationDto>> Handle(StartApplicationCommand command, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var existing = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: false, ct);
        if (existing is not null)
        {
            return AdmissionSupport.ToMine(existing, now);
        }

        var period = await AdmissionSupport.OpenPeriodAsync(db, now, ct);
        if (period is null)
        {
            return Error.Conflict("admissions.not_open", "There is no admission period open at the moment.");
        }

        var applicant = Applicant.Create(currentUser.UserId, period.Id);
        applicant.SaveForm(AdmissionSupport.Serialize(ApplicantFormDto.Empty with { FullName = currentUser.FullName }));
        db.Applicants.Add(applicant);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same person started it twice at once; the unique index keeps one.
            db.ChangeTracker.Clear();
        }

        var saved = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: false, ct);
        return AdmissionSupport.ToMine(saved!, now);
    }
}
