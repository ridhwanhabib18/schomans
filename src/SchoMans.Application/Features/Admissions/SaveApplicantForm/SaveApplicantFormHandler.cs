using FluentValidation;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Admissions.SaveApplicantForm;

/// <summary>The form is saved as a draft: fields may still be empty; the application can only be handed in once all are filled.</summary>
public sealed record SaveApplicantFormCommand(ApplicantFormDto Form);

public sealed class SaveApplicantFormValidator : AbstractValidator<SaveApplicantFormCommand>
{
    public SaveApplicantFormValidator()
    {
        RuleFor(x => x.Form).NotNull().ChildRules(form =>
        {
            form.RuleFor(f => f.FullName).MaximumLength(200);
            form.RuleFor(f => f.PlaceOfBirth).MaximumLength(100);
            form.RuleFor(f => f.Address).MaximumLength(300);
            form.RuleFor(f => f.GuardianName).MaximumLength(200);
            form.RuleFor(f => f.GuardianPhone).MaximumLength(30).Matches(@"^[0-9+()\- ]*$").WithMessage("A phone number has digits, spaces and + ( ) - only.");
            form.RuleFor(f => f.PreviousSchool).MaximumLength(200);
            form.RuleFor(f => f.DateOfBirth)
                .Must(date => date is null || (date < DateOnly.FromDateTime(DateTime.UtcNow.Date) && date > new DateOnly(1980, 1, 1)))
                .WithMessage("The date of birth must be in the past and plausible.");
        });
    }
}

public sealed class SaveApplicantFormHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<MyApplicationDto>> Handle(SaveApplicantFormCommand command, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var applicant = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: true, ct);
        if (applicant is null)
        {
            return AdmissionSupport.NoApplication();
        }

        if (!applicant.AdmissionPeriod!.IsOpenAt(now) || !applicant.SaveForm(AdmissionSupport.Serialize(Trim(command.Form))))
        {
            return Error.Conflict("admissions.not_editable", "The application can no longer be changed.");
        }

        await db.SaveChangesAsync(ct);
        return AdmissionSupport.ToMine(applicant, now);
    }

    private static ApplicantFormDto Trim(ApplicantFormDto form) => form with
    {
        FullName = (form.FullName ?? string.Empty).Trim(),
        PlaceOfBirth = (form.PlaceOfBirth ?? string.Empty).Trim(),
        Address = (form.Address ?? string.Empty).Trim(),
        GuardianName = (form.GuardianName ?? string.Empty).Trim(),
        GuardianPhone = (form.GuardianPhone ?? string.Empty).Trim(),
        PreviousSchool = (form.PreviousSchool ?? string.Empty).Trim(),
    };
}
