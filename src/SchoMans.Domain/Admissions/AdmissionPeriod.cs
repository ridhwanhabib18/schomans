using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Admissions;

/// <summary>
/// One round of admissions for an academic year: applicants register while it is open, the results are announced at one
/// moment for everybody, and the accepted ones confirm their place (re-registration) before the deadline.
/// </summary>
public sealed class AdmissionPeriod : BaseEntity
{
    public const int NameMaxLength = 100;

    private AdmissionPeriod()
    {
    }

    public Guid AcademicYearId { get; private set; }

    public AcademicYear? AcademicYear { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset OpensAt { get; private set; }

    public DateTimeOffset ClosesAt { get; private set; }

    public DateTimeOffset ResultsAnnouncedAt { get; private set; }

    public DateTimeOffset ReregistrationDeadline { get; private set; }

    /// <summary>Set by the job that told the applicants the results are out; it keeps the job from telling them twice.</summary>
    public DateTimeOffset? ResultsNotifiedAt { get; private set; }

    public static AdmissionPeriod Create(
        Guid academicYearId,
        string name,
        DateTimeOffset opensAt,
        DateTimeOffset closesAt,
        DateTimeOffset resultsAnnouncedAt,
        DateTimeOffset reregistrationDeadline)
    {
        var period = new AdmissionPeriod { AcademicYearId = academicYearId };
        period.Update(name, opensAt, closesAt, resultsAnnouncedAt, reregistrationDeadline);
        return period;
    }

    public void Update(string name, DateTimeOffset opensAt, DateTimeOffset closesAt, DateTimeOffset resultsAnnouncedAt, DateTimeOffset reregistrationDeadline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Trim().Length > NameMaxLength)
        {
            throw new ArgumentException($"A name is at most {NameMaxLength} characters.", nameof(name));
        }

        if (!(opensAt < closesAt && closesAt < resultsAnnouncedAt && resultsAnnouncedAt < reregistrationDeadline))
        {
            throw new ArgumentException("The dates must run in order: opens, closes, results announced, re-registration deadline.");
        }

        Name = name.Trim();
        OpensAt = opensAt;
        ClosesAt = closesAt;
        ResultsAnnouncedAt = resultsAnnouncedAt;
        ReregistrationDeadline = reregistrationDeadline;
    }

    /// <summary>Applicants may register, fill in the form and upload documents from the opening to the closing.</summary>
    public bool IsOpenAt(DateTimeOffset now) => now >= OpensAt && now < ClosesAt;

    /// <summary>Everybody sees the results from the same moment; before it the decision stays hidden.</summary>
    public bool ResultsVisibleAt(DateTimeOffset now) => now >= ResultsAnnouncedAt;

    public bool ReregistrationOpenAt(DateTimeOffset now) => ResultsVisibleAt(now) && now < ReregistrationDeadline;

    public void MarkResultsNotified(DateTimeOffset now) => ResultsNotifiedAt = now;
}
