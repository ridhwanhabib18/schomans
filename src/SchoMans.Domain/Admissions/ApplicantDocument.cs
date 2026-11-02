using SchoMans.Domain.Common;

namespace SchoMans.Domain.Admissions;

/// <summary>A document of an applicant (birth certificate, family card, report card). Staff verify it or reject it with a note; a rejected one can be uploaded again.</summary>
public sealed class ApplicantDocument : BaseEntity
{
    public const int FileNameMaxLength = 200;
    public const int NoteMaxLength = 500;

    private ApplicantDocument()
    {
    }

    public Guid ApplicantId { get; private set; }

    public DocumentType Type { get; private set; }

    public string FileKey { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public DocumentStatus Status { get; private set; } = DocumentStatus.Pending;

    public string? ReviewNote { get; private set; }

    public static ApplicantDocument Create(Guid applicantId, DocumentType type, string fileKey, string fileName)
    {
        var document = new ApplicantDocument { ApplicantId = applicantId, Type = type };
        document.SetFile(fileKey, fileName);
        return document;
    }

    /// <summary>A new file for this document starts the review again.</summary>
    public void Replace(string fileKey, string fileName)
    {
        SetFile(fileKey, fileName);
        Status = DocumentStatus.Pending;
        ReviewNote = null;
    }

    public void Verify()
    {
        Status = DocumentStatus.Verified;
        ReviewNote = null;
    }

    public void Reject(string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        if (note.Trim().Length > NoteMaxLength)
        {
            throw new ArgumentException($"A note is at most {NoteMaxLength} characters.", nameof(note));
        }

        Status = DocumentStatus.Rejected;
        ReviewNote = note.Trim();
    }

    private void SetFile(string fileKey, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        FileKey = fileKey;
        FileName = fileName.Trim().Length > FileNameMaxLength ? fileName.Trim()[..FileNameMaxLength] : fileName.Trim();
    }
}
