using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Admissions.SubmitApplication;

public sealed record SubmitApplicationCommand;

/// <summary>The applicant hands the application in: the form is complete, all three documents are there and the period is still open. The admin staff are told.</summary>
public sealed class SubmitApplicationHandler(IAppDbContext db, ICurrentUser currentUser, INotificationService notifications, ISchoolClock clock)
{
    public async Task<Result<MyApplicationDto>> Handle(SubmitApplicationCommand command, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var applicant = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: true, ct);
        if (applicant is null)
        {
            return AdmissionSupport.NoApplication();
        }

        if (!applicant.IsEditable || !applicant.AdmissionPeriod!.IsOpenAt(now))
        {
            return Error.Conflict("admissions.not_editable", "The application can no longer be handed in.");
        }

        if (!AdmissionSupport.ReadForm(applicant.FormData).IsComplete)
        {
            return Error.Validation("admissions.form_incomplete", "Fill in every field of the form first.");
        }

        if (!applicant.Submit(now))
        {
            return Error.Validation("admissions.documents_missing", "Upload the birth certificate, the family card and the report card first.");
        }

        await db.SaveChangesAsync(ct);

        var admins = await db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync(ct);
        await NotificationSender.SendAsync(
            db,
            notifications,
            admins,
            NotificationType.AdmissionSubmitted,
            "New application to verify",
            $"{AdmissionSupport.ReadForm(applicant.FormData).FullName} handed in an application.",
            $"/manage/admissions/{applicant.Id}",
            ct);

        return AdmissionSupport.ToMine(applicant, now);
    }
}
