using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments;
using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions;

/// <summary>Shared pieces of the admissions use cases: the form as JSON, the files of the documents, and the mapping to DTOs.</summary>
internal static class AdmissionSupport
{
    public static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Serialize(ApplicantFormDto form) => JsonSerializer.Serialize(form, Json);

    public static ApplicantFormDto ReadForm(string formData)
    {
        try
        {
            return JsonSerializer.Deserialize<ApplicantFormDto>(formData, Json) ?? ApplicantFormDto.Empty;
        }
        catch (JsonException)
        {
            return ApplicantFormDto.Empty;
        }
    }

    public static string NewDocumentKey(Guid applicantId, DocumentType type, string contentType) =>
        $"admissions/{applicantId}/{type}/{Guid.NewGuid():N}.{AssignmentFileRules.SubmissionTypes[contentType]}";

    public static bool IsDocumentKeyOf(string key, Guid applicantId, DocumentType type) =>
        key.StartsWith($"admissions/{applicantId}/{type}/", StringComparison.Ordinal) && !key.Contains("..", StringComparison.Ordinal);

    /// <summary>The applicant of the signed-in user, with the documents and the period.</summary>
    public static Task<Applicant?> OwnApplicationAsync(IAppDbContext db, ICurrentUser user, bool track, CancellationToken ct)
    {
        var query = db.Applicants.Include(a => a.Documents).Include(a => a.AdmissionPeriod).AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.Where(a => a.UserId == user.UserId).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);
    }

    public static PublicAdmissionPeriodDto ToPublic(AdmissionPeriod period, DateTimeOffset now) => new(
        period.Id, period.Name, period.OpensAt, period.ClosesAt, period.ResultsAnnouncedAt, period.ReregistrationDeadline, period.IsOpenAt(now));

    public static MyApplicationDto ToMine(Applicant applicant, DateTimeOffset now)
    {
        var period = applicant.AdmissionPeriod!;
        var resultsVisible = period.ResultsVisibleAt(now);
        var status = applicant.VisibleStatus(resultsVisible);
        var form = ReadForm(applicant.FormData);
        var decided = applicant.Status is ApplicantStatus.Accepted or ApplicantStatus.Rejected or ApplicantStatus.Enrolled;

        return new MyApplicationDto(
            applicant.Id,
            status,
            form,
            decided && !resultsVisible ? null : applicant.Notes,
            applicant.SubmittedAt,
            ToDocuments(applicant),
            CanEdit: applicant.IsEditable && period.IsOpenAt(now),
            CanSubmit: applicant.IsEditable && period.IsOpenAt(now) && applicant.HasAllDocuments && form.IsComplete,
            CanReplaceRejectedDocuments: applicant.Status is ApplicantStatus.Submitted or ApplicantStatus.InVerification && applicant.HasRejectedDocument,
            ResultsVisible: resultsVisible,
            CanConfirmReregistration: applicant.Status == ApplicantStatus.Accepted && applicant.ReregistrationConfirmedAt is null && period.ReregistrationOpenAt(now),
            ReregistrationConfirmed: applicant.ReregistrationConfirmedAt is not null);
    }

    public static IReadOnlyList<ApplicantDocumentDto> ToDocuments(Applicant applicant) =>
        applicant.Documents.OrderBy(d => d.Type).Select(d => new ApplicantDocumentDto(d.Type, d.FileName, d.Status, d.ReviewNote)).ToList();

    public static ApplicantDetailDto ToDetail(Applicant applicant, string email) => new(
        applicant.Id,
        email,
        applicant.Status,
        ReadForm(applicant.FormData),
        applicant.Notes,
        applicant.SubmittedAt,
        applicant.DecidedAt,
        applicant.ReregistrationConfirmedAt,
        applicant.StudentId,
        applicant.AdmissionPeriod!.Name,
        ToDocuments(applicant));

    /// <summary>The period an application is made in: the one open now. Null when none is open.</summary>
    public static Task<AdmissionPeriod?> OpenPeriodAsync(IAppDbContext db, DateTimeOffset now, CancellationToken ct) =>
        db.AdmissionPeriods.AsNoTracking().Where(p => p.OpensAt <= now && now < p.ClosesAt).OrderByDescending(p => p.OpensAt).FirstOrDefaultAsync(ct);

    public static Error NoApplication() => Error.NotFound("admissions.no_application", "You have not started an application.");
}
