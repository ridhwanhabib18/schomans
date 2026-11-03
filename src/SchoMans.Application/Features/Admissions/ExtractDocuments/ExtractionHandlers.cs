using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Admissions.Staff;
using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions.ExtractDocuments;

public sealed record ExtractMyDocumentCommand(DocumentType Type);

public sealed record ExtractApplicantDocumentCommand(Guid ApplicantId, DocumentType Type);

public sealed record GetApplicantExtractionsQuery(Guid ApplicantId);

/// <summary>An applicant has the AI read one of their own documents; they then confirm what it found in the form themselves.</summary>
public sealed class ExtractMyDocumentHandler(IAppDbContext db, ICurrentUser currentUser, DocumentExtractor extractor)
{
    public async Task<Result<DocumentExtractionDto>> Handle(ExtractMyDocumentCommand command, CancellationToken ct)
    {
        var applicant = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: true, ct);
        return applicant is null
            ? AdmissionSupport.NoApplication()
            : await extractor.ExtractAsync(applicant, command.Type, currentUser.UserId, ct);
    }
}

/// <summary>Admin staff have the AI read a document of any applicant, to check it while verifying.</summary>
public sealed class ExtractApplicantDocumentHandler(IAppDbContext db, ICurrentUser currentUser, DocumentExtractor extractor)
{
    public async Task<Result<DocumentExtractionDto>> Handle(ExtractApplicantDocumentCommand command, CancellationToken ct)
    {
        var applicant = await ApplicantQueries.FindAsync(db, command.ApplicantId, track: true, ct);
        return applicant is null
            ? ApplicantQueries.NotFound()
            : await extractor.ExtractAsync(applicant, command.Type, currentUser.UserId, ct);
    }
}

public sealed class GetMyExtractionsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ApplicantExtractionsDto>> Handle(CancellationToken ct)
    {
        var applicant = await AdmissionSupport.OwnApplicationAsync(db, currentUser, track: false, ct);
        return applicant is null ? AdmissionSupport.NoApplication() : DocumentExtractor.Summarize(applicant);
    }
}

public sealed class GetApplicantExtractionsHandler(IAppDbContext db)
{
    public async Task<Result<ApplicantExtractionsDto>> Handle(GetApplicantExtractionsQuery query, CancellationToken ct)
    {
        var applicant = await ApplicantQueries.FindAsync(db, query.ApplicantId, track: false, ct);
        return applicant is null ? ApplicantQueries.NotFound() : DocumentExtractor.Summarize(applicant);
    }
}
