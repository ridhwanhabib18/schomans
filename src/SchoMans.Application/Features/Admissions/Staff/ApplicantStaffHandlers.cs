using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments;
using SchoMans.Domain.Admissions;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Admissions.Staff;

public sealed record ListApplicantsQuery(Guid? PeriodId = null, ApplicantStatus? Status = null, string? Search = null, int Page = 1, int PageSize = 20);

public sealed record GetApplicantQuery(Guid Id);

public sealed record GetApplicantDocumentUrlQuery(Guid ApplicantId, DocumentType Type);

/// <param name="Verified">True verifies the document; false rejects it, and then the note tells the applicant what to fix.</param>
public sealed record ReviewDocumentCommand(Guid ApplicantId, DocumentType Type, bool Verified, string? Note);

/// <param name="Accept">True accepts (all documents must be verified); false rejects, and then the note is needed.</param>
public sealed record DecideApplicantCommand(Guid ApplicantId, bool Accept, string? Note);

/// <summary>Enrolls an accepted applicant who confirmed the place: a student record in the chosen classroom with the given NISN.</summary>
public sealed record EnrollApplicantCommand(Guid ApplicantId, Guid ClassroomId, string Nisn);

public sealed class ListApplicantsValidator : AbstractValidator<ListApplicantsQuery>
{
    public ListApplicantsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class ReviewDocumentValidator : AbstractValidator<ReviewDocumentCommand>
{
    public ReviewDocumentValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Verified).WithMessage("Tell the applicant what is wrong with the document.");
        RuleFor(x => x.Note).MaximumLength(ApplicantDocument.NoteMaxLength);
    }
}

public sealed class DecideApplicantValidator : AbstractValidator<DecideApplicantCommand>
{
    public DecideApplicantValidator()
    {
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Accept).WithMessage("Give the reason for the rejection.");
        RuleFor(x => x.Note).MaximumLength(Applicant.NotesMaxLength);
    }
}

public sealed class EnrollApplicantValidator : AbstractValidator<EnrollApplicantCommand>
{
    public EnrollApplicantValidator()
    {
        RuleFor(x => x.ClassroomId).NotEmpty();
        RuleFor(x => x.Nisn).MustBeNisn();
    }
}

internal static class ApplicantQueries
{
    public static Task<Applicant?> FindAsync(IAppDbContext db, Guid id, bool track, CancellationToken ct)
    {
        var query = db.Applicants.Include(a => a.Documents).Include(a => a.AdmissionPeriod).Include(a => a.User).AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public static Error NotFound() => Error.NotFound("admissions.applicant_not_found", "The application does not exist.");
}

public sealed class ListApplicantsHandler(IAppDbContext db)
{
    public async Task<Result<PagedResult<ApplicantListItemDto>>> Handle(ListApplicantsQuery query, CancellationToken ct)
    {
        var applicants = db.Applicants.AsNoTracking().AsQueryable();
        applicants = query.Status is { } status
            ? applicants.Where(a => a.Status == status)
            : applicants.Where(a => a.Status != ApplicantStatus.Draft);

        if (query.PeriodId is { } periodId)
        {
            applicants = applicants.Where(a => a.AdmissionPeriodId == periodId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            applicants = applicants.Where(a => a.FormData.Contains(term) || a.User!.Email.Contains(term) || a.User.FullName.Contains(term));
        }

        var page = await applicants
            .OrderBy(a => a.SubmittedAt)
            .ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.FormData,
                a.User!.Email,
                a.User.FullName,
                a.Status,
                Verified = a.Documents.Count(d => d.Status == DocumentStatus.Verified),
                Total = a.Documents.Count,
                a.SubmittedAt,
                PeriodName = a.AdmissionPeriod!.Name,
            })
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        var items = page.Items
            .Select(a =>
            {
                var name = AdmissionSupport.ReadForm(a.FormData).FullName;
                return new ApplicantListItemDto(a.Id, string.IsNullOrWhiteSpace(name) ? a.FullName : name, a.Email, a.Status, a.Verified, a.Total, a.SubmittedAt, a.PeriodName);
            })
            .ToList();
        return new PagedResult<ApplicantListItemDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetApplicantHandler(IAppDbContext db)
{
    public async Task<Result<ApplicantDetailDto>> Handle(GetApplicantQuery query, CancellationToken ct)
    {
        var applicant = await ApplicantQueries.FindAsync(db, query.Id, track: false, ct);
        return applicant is null ? ApplicantQueries.NotFound() : AdmissionSupport.ToDetail(applicant, applicant.User!.Email);
    }
}

public sealed class GetApplicantDocumentUrlHandler(IAppDbContext db, IFileStorage storage, TimeProvider timeProvider)
{
    public async Task<Result<DownloadUrlDto>> Handle(GetApplicantDocumentUrlQuery query, CancellationToken ct)
    {
        var key = await db.ApplicantDocuments.AsNoTracking()
            .Where(d => d.ApplicantId == query.ApplicantId && d.Type == query.Type)
            .Select(d => d.FileKey)
            .FirstOrDefaultAsync(ct);
        if (key is null)
        {
            return Error.NotFound("admissions.document_not_found", "The document was not uploaded.");
        }

        return new DownloadUrlDto(storage.CreateDownloadUrl(key, AdmissionSupport.DownloadLifetime), timeProvider.GetUtcNow() + AdmissionSupport.DownloadLifetime);
    }
}

public sealed class ReviewDocumentHandler(IAppDbContext db, INotificationService notifications)
{
    public async Task<Result<ApplicantDetailDto>> Handle(ReviewDocumentCommand command, CancellationToken ct)
    {
        var applicant = await ApplicantQueries.FindAsync(db, command.ApplicantId, track: true, ct);
        if (applicant is null)
        {
            return ApplicantQueries.NotFound();
        }

        var document = applicant.Documents.FirstOrDefault(d => d.Type == command.Type);
        if (document is null)
        {
            return Error.NotFound("admissions.document_not_found", "The document was not uploaded.");
        }

        if (applicant.Status is not (ApplicantStatus.Submitted or ApplicantStatus.InVerification))
        {
            return Error.Conflict("admissions.not_in_review", "Only an application in verification has documents to review.");
        }

        if (command.Verified)
        {
            document.Verify();
        }
        else
        {
            document.Reject(command.Note!);
        }

        applicant.BeginVerification();
        await db.SaveChangesAsync(ct);

        if (!command.Verified)
        {
            await NotificationSender.SendAsync(
                db,
                notifications,
                [applicant.UserId],
                NotificationType.AdmissionDocumentReviewed,
                "A document needs a new upload",
                command.Note!,
                "/admissions",
                ct);
        }

        return AdmissionSupport.ToDetail(applicant, applicant.User!.Email);
    }
}

public sealed class DecideApplicantHandler(IAppDbContext db, ISchoolClock clock)
{
    public async Task<Result<ApplicantDetailDto>> Handle(DecideApplicantCommand command, CancellationToken ct)
    {
        var applicant = await ApplicantQueries.FindAsync(db, command.ApplicantId, track: true, ct);
        if (applicant is null)
        {
            return ApplicantQueries.NotFound();
        }

        var now = clock.UtcNow;
        if (command.Accept)
        {
            if (applicant.Status is not (ApplicantStatus.Submitted or ApplicantStatus.InVerification))
            {
                return Error.Conflict("admissions.not_in_review", "Only an application in verification can be decided.");
            }

            applicant.BeginVerification();
            if (!applicant.Accept(command.Note, now))
            {
                return Error.Conflict("admissions.documents_not_verified", "Verify all three documents before accepting.");
            }
        }
        else if (!applicant.Reject(command.Note!, now))
        {
            return Error.Conflict("admissions.not_in_review", "Only an application in verification can be decided.");
        }

        await db.SaveChangesAsync(ct);
        return AdmissionSupport.ToDetail(applicant, applicant.User!.Email);
    }
}

public sealed class EnrollApplicantHandler(IAppDbContext db, IIdentityProvisioner identity, INotificationService notifications)
{
    public async Task<Result<ApplicantDetailDto>> Handle(EnrollApplicantCommand command, CancellationToken ct)
    {
        var applicant = await ApplicantQueries.FindAsync(db, command.ApplicantId, track: true, ct);
        if (applicant is null)
        {
            return ApplicantQueries.NotFound();
        }

        if (applicant.Status != ApplicantStatus.Accepted || applicant.ReregistrationConfirmedAt is null)
        {
            return Error.Conflict("admissions.not_confirmed", "Only an accepted applicant who confirmed the place can be enrolled.");
        }

        if (!await db.Classrooms.AnyAsync(c => c.Id == command.ClassroomId, ct))
        {
            return Error.NotFound("classrooms.not_found", "The classroom does not exist.");
        }

        var nisn = command.Nisn.Trim();
        if (await db.Students.AnyAsync(s => s.Nisn == nisn, ct))
        {
            return Error.Conflict("students.nisn_taken", "Another student already has this NISN.");
        }

        var form = AdmissionSupport.ReadForm(applicant.FormData);
        var user = applicant.User!;
        if (form.DateOfBirth is null || user.KeycloakId is null)
        {
            return Error.Conflict("admissions.cannot_enroll", "The applicant has no date of birth or has not signed in yet.");
        }

        // The role lives in Keycloak first: a login overwrites the role in the database with the one of the token.
        var promoted = await identity.PromoteApplicantToStudentAsync(user.KeycloakId, ct);
        if (promoted.IsFailure)
        {
            return promoted.Error;
        }

        user.SyncProfile(user.Email, user.FullName, UserRole.Student);
        var student = Student.Create(user.Id, nisn, command.ClassroomId, form.DateOfBirth.Value);
        db.Students.Add(student);
        applicant.Enroll(student.Id);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("students.duplicate", "A student with this NISN or account was just created.");
        }

        await NotificationSender.SendAsync(
            db,
            notifications,
            [user.Id],
            NotificationType.AdmissionResult,
            "Welcome to the school",
            "You are enrolled as a student. Sign in again to see your pages.",
            "/dashboard",
            ct);

        return AdmissionSupport.ToDetail(applicant, user.Email);
    }
}
