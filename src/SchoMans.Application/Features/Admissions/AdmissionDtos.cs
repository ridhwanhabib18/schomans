using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions;

/// <summary>What anybody may know about a period: the banner of the landing page and the start of an application.</summary>
public sealed record PublicAdmissionPeriodDto(
    Guid Id,
    string Name,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    DateTimeOffset ResultsAnnouncedAt,
    DateTimeOffset ReregistrationDeadline,
    bool IsOpen);

/// <summary>The period that is open, or else the next one that will open. Null when the school has none to show.</summary>
public sealed record OpenAdmissionDto(PublicAdmissionPeriodDto? Period);

/// <summary>The registration form. Every field is needed before the application can be handed in.</summary>
public sealed record ApplicantFormDto(
    string FullName,
    DateOnly? DateOfBirth,
    string PlaceOfBirth,
    string Address,
    string GuardianName,
    string GuardianPhone,
    string PreviousSchool)
{
    public static readonly ApplicantFormDto Empty = new(string.Empty, null, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(FullName)
        && DateOfBirth is not null
        && !string.IsNullOrWhiteSpace(PlaceOfBirth)
        && !string.IsNullOrWhiteSpace(Address)
        && !string.IsNullOrWhiteSpace(GuardianName)
        && !string.IsNullOrWhiteSpace(GuardianPhone)
        && !string.IsNullOrWhiteSpace(PreviousSchool);
}

public sealed record ApplicantDocumentDto(DocumentType Type, string FileName, DocumentStatus Status, string? ReviewNote);

/// <summary>
/// The application as its owner sees it. Until the results are announced a decision shows as "in verification" and its
/// note stays hidden; a rejected document and its note are always shown, because the applicant has to act on them.
/// </summary>
public sealed record MyApplicationDto(
    Guid Id,
    ApplicantStatus Status,
    ApplicantFormDto Form,
    string? Notes,
    DateTimeOffset? SubmittedAt,
    IReadOnlyList<ApplicantDocumentDto> Documents,
    bool CanEdit,
    bool CanSubmit,
    bool CanReplaceRejectedDocuments,
    bool ResultsVisible,
    bool CanConfirmReregistration,
    bool ReregistrationConfirmed);

public sealed record MyAdmissionDto(PublicAdmissionPeriodDto? Period, MyApplicationDto? Application);

/// <summary>A period as the admin staff manage it.</summary>
public sealed record AdmissionPeriodDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    string AcademicYearName,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    DateTimeOffset ResultsAnnouncedAt,
    DateTimeOffset ReregistrationDeadline,
    int ApplicantCount);

public sealed record ApplicantListItemDto(
    Guid Id,
    string FullName,
    string Email,
    ApplicantStatus Status,
    int VerifiedDocuments,
    int TotalDocuments,
    DateTimeOffset? SubmittedAt,
    string PeriodName);

/// <summary>An application as the admin staff see it: the real status, never hidden.</summary>
public sealed record ApplicantDetailDto(
    Guid Id,
    string Email,
    ApplicantStatus Status,
    ApplicantFormDto Form,
    string? Notes,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? ReregistrationConfirmedAt,
    Guid? StudentId,
    string PeriodName,
    IReadOnlyList<ApplicantDocumentDto> Documents);
