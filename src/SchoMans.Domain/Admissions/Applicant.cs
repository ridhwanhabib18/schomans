using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Admissions;

/// <summary>
/// A person applying for a place: draft, submitted, in verification, then accepted or rejected, and finally enrolled as
/// a student. The decision is made when staff decide, but the applicant sees it only from the announcement moment of the period.
/// </summary>
public sealed class Applicant : BaseEntity
{
    public const int FormDataMaxLength = 4_000;
    public const int NotesMaxLength = 1_000;

    private Applicant()
    {
    }

    public Guid UserId { get; private set; }

    public User? User { get; private set; }

    public Guid AdmissionPeriodId { get; private set; }

    public AdmissionPeriod? AdmissionPeriod { get; private set; }

    /// <summary>The registration form as JSON; its shape belongs to the use case, the domain only keeps the text.</summary>
    public string FormData { get; private set; } = "{}";

    public ApplicantStatus Status { get; private set; } = ApplicantStatus.Draft;

    /// <summary>The staff's note for the applicant, for example why the application was rejected.</summary>
    public string? Notes { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public DateTimeOffset? ReregistrationConfirmedAt { get; private set; }

    public Guid? StudentId { get; private set; }

    public Student? Student { get; private set; }

    public List<ApplicantDocument> Documents { get; private set; } = [];

    public static Applicant Create(Guid userId, Guid admissionPeriodId) => new() { UserId = userId, AdmissionPeriodId = admissionPeriodId };

    public bool IsEditable => Status == ApplicantStatus.Draft;

    public bool HasAllDocuments => Enum.GetValues<DocumentType>().All(type => Documents.Any(d => d.Type == type));

    public bool AllDocumentsVerified => HasAllDocuments && Documents.All(d => d.Status == DocumentStatus.Verified);

    public bool HasRejectedDocument => Documents.Any(d => d.Status == DocumentStatus.Rejected);

    /// <summary>The form can be changed while the application is a draft. Returns false afterwards.</summary>
    public bool SaveForm(string formData)
    {
        ArgumentNullException.ThrowIfNull(formData);

        if (formData.Length > FormDataMaxLength)
        {
            throw new ArgumentException($"The form is at most {FormDataMaxLength} characters.", nameof(formData));
        }

        if (!IsEditable)
        {
            return false;
        }

        FormData = formData;
        return true;
    }

    /// <summary>Hands the application in. It needs all three documents; returns false when it was not a draft or documents are missing.</summary>
    public bool Submit(DateTimeOffset now)
    {
        if (!IsEditable || !HasAllDocuments)
        {
            return false;
        }

        Status = ApplicantStatus.Submitted;
        SubmittedAt = now;
        return true;
    }

    /// <summary>Staff start looking at the application.</summary>
    public void BeginVerification()
    {
        if (Status == ApplicantStatus.Submitted)
        {
            Status = ApplicantStatus.InVerification;
        }
    }

    /// <summary>Staff accept: every document must be verified first. Returns false otherwise.</summary>
    public bool Accept(string? note, DateTimeOffset now)
    {
        if (Status != ApplicantStatus.InVerification || !AllDocumentsVerified)
        {
            return false;
        }

        Decide(ApplicantStatus.Accepted, note, now);
        return true;
    }

    /// <summary>Staff reject, with a note for the applicant.</summary>
    public bool Reject(string note, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        if (Status is not (ApplicantStatus.Submitted or ApplicantStatus.InVerification))
        {
            return false;
        }

        Decide(ApplicantStatus.Rejected, note, now);
        return true;
    }

    /// <summary>The accepted applicant confirms the place (re-registration).</summary>
    public bool ConfirmReregistration(DateTimeOffset now)
    {
        if (Status != ApplicantStatus.Accepted || ReregistrationConfirmedAt is not null)
        {
            return false;
        }

        ReregistrationConfirmedAt = now;
        return true;
    }

    /// <summary>Staff enrolled the applicant as a student; only a confirmed place can be enrolled.</summary>
    public bool Enroll(Guid studentId)
    {
        if (Status != ApplicantStatus.Accepted || ReregistrationConfirmedAt is null)
        {
            return false;
        }

        StudentId = studentId;
        Status = ApplicantStatus.Enrolled;
        return true;
    }

    /// <summary>
    /// What the applicant is allowed to see: until the results are announced a decision is hidden behind "in verification".
    /// </summary>
    public ApplicantStatus VisibleStatus(bool resultsVisible) =>
        !resultsVisible && Status is ApplicantStatus.Accepted or ApplicantStatus.Rejected
            ? ApplicantStatus.InVerification
            : Status;

    private void Decide(ApplicantStatus status, string? note, DateTimeOffset now)
    {
        if (note is { Length: > NotesMaxLength })
        {
            throw new ArgumentException($"A note is at most {NotesMaxLength} characters.", nameof(note));
        }

        Status = status;
        Notes = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedAt = now;
    }
}
