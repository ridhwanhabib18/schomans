using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Grading;

/// <summary>The term report card of a student. The PDF is generated on demand and kept in storage under <see cref="PdfKey"/>.</summary>
public sealed class ReportCard : BaseEntity
{
    private ReportCard()
    {
    }

    public Guid StudentId { get; private set; }

    public Student? Student { get; private set; }

    public Guid TermId { get; private set; }

    public Term? Term { get; private set; }

    public ReportCardStatus Status { get; private set; } = ReportCardStatus.Draft;

    public string? PdfKey { get; private set; }

    public DateTimeOffset? GeneratedAt { get; private set; }

    public List<AchievementDescription> Descriptions { get; private set; } = [];

    public static ReportCard Create(Guid studentId, Guid termId) => new() { StudentId = studentId, TermId = termId };

    public void AttachPdf(string pdfKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfKey);

        PdfKey = pdfKey;
        GeneratedAt = now;
    }

    public void Publish() => Status = ReportCardStatus.Published;

    public void Unpublish() => Status = ReportCardStatus.Draft;
}
