using FluentValidation;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.Files.CreateUploadUrl;
using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions.ApplicantDocuments;

/// <summary>Step 1 of an upload: a presigned URL for the applicant's browser (PDF or image, at most 10 MB).</summary>
public sealed record CreateDocumentUploadUrlCommand(DocumentType Type, string ContentType, long SizeBytes);

public sealed class CreateDocumentUploadUrlValidator : AbstractValidator<CreateDocumentUploadUrlCommand>
{
    public CreateDocumentUploadUrlValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.ContentType)
            .Must(type => AssignmentFileRules.SubmissionTypes.ContainsKey(type ?? string.Empty))
            .WithMessage("Upload a PDF or an image (JPEG, PNG, WebP).");
        RuleFor(x => x.SizeBytes)
            .InclusiveBetween(1, AssignmentFileRules.MaxBytes)
            .WithMessage("The file must be larger than 0 and at most 10 MB.");
    }
}

/// <summary>Step 2: the file is in storage; attach it to the application as that kind of document.</summary>
public sealed record AttachDocumentCommand(DocumentType Type, string FileKey, string FileName);

public sealed class AttachDocumentValidator : AbstractValidator<AttachDocumentCommand>
{
    public AttachDocumentValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.FileKey).NotEmpty().MaximumLength(300);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(ApplicantDocument.FileNameMaxLength);
    }
}

internal static class DocumentRules
{
    /// <summary>
    /// While the application is a draft every document can be set or replaced. Once handed in, only a document staff
    /// rejected can be uploaded again; everything else is final until staff decide.
    /// </summary>
    public static bool MayChange(Applicant applicant, DocumentType type, AdmissionPeriod period, DateTimeOffset now) =>
        applicant.IsEditable
            ? period.IsOpenAt(now)
            : applicant.Status is ApplicantStatus.Submitted or ApplicantStatus.InVerification
                && applicant.Documents.Any(d => d.Type == type && d.Status == DocumentStatus.Rejected);
}

public sealed class CreateDocumentUploadUrlHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, ISchoolClock clock)
{
    public async Task<Result<UploadUrlDto>> Handle(CreateDocumentUploadUrlCommand command, CancellationToken ct)
    {
        var applicant = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: false, ct);
        if (applicant is null)
        {
            return AdmissionSupport.NoApplication();
        }

        if (!DocumentRules.MayChange(applicant, command.Type, applicant.AdmissionPeriod!, clock.UtcNow))
        {
            return Error.Conflict("admissions.document_locked", "This document can no longer be changed.");
        }

        var key = AdmissionSupport.NewDocumentKey(applicant.Id, command.Type, command.ContentType);
        var upload = await storage.CreateUploadUrlAsync(FileBucket.Private, key, command.ContentType, AdmissionSupport.UploadLifetime, ct);
        return new UploadUrlDto(upload.Url, key, command.ContentType, upload.ExpiresAt);
    }
}

public sealed class AttachDocumentHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, ISchoolClock clock)
{
    public async Task<Result<MyApplicationDto>> Handle(AttachDocumentCommand command, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var applicant = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: true, ct);
        if (applicant is null)
        {
            return AdmissionSupport.NoApplication();
        }

        if (!DocumentRules.MayChange(applicant, command.Type, applicant.AdmissionPeriod!, now))
        {
            return Error.Conflict("admissions.document_locked", "This document can no longer be changed.");
        }

        var key = command.FileKey.Trim();
        if (!AdmissionSupport.IsDocumentKeyOf(key, applicant.Id, command.Type))
        {
            return Error.Validation("files.invalid_key", "The file does not belong to this document.");
        }

        var problem = await AssignmentFiles.VerifyAsync(storage, key, AssignmentFileRules.SubmissionTypes, ct);
        if (problem is not null)
        {
            return problem;
        }

        var existing = applicant.Documents.FirstOrDefault(d => d.Type == command.Type);
        var oldKey = existing?.FileKey;
        if (existing is null)
        {
            applicant.Documents.Add(ApplicantDocument.Create(applicant.Id, command.Type, key, command.FileName));
        }
        else
        {
            existing.Replace(key, command.FileName);
        }

        await db.SaveChangesAsync(ct);

        if (oldKey is not null && oldKey != key)
        {
            await storage.DeleteAsync(FileBucket.Private, oldKey, ct);
        }

        return AdmissionSupport.ToMine(applicant, now);
    }
}
