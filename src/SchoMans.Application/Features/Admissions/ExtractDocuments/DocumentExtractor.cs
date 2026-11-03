using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions.ExtractDocuments;

/// <summary>
/// AI-03: reads the photo of one document with the AI and keeps what it read on the document. Used for an applicant's own
/// document and by staff for any applicant's. It is a suggestion: nothing in the application form changes by itself.
/// The model sees the photo itself, so a name cannot be replaced by a code here (ADR-004): with Ollama it stays on the machine.
/// </summary>
public sealed class DocumentExtractor(IAppDbContext db, IFileStorage storage, AiGateway gateway, ISchoolClock clock)
{
    /// <summary>Claude takes images of at most 5 MB.</summary>
    public const int MaxImageBytes = 5 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["webp"] = "image/webp",
    };

    /// <param name="applicant">Loaded with its documents and tracked.</param>
    /// <param name="userId">Who asked; the call counts toward their daily limit.</param>
    public async Task<Result<DocumentExtractionDto>> ExtractAsync(Applicant applicant, DocumentType type, Guid userId, CancellationToken ct)
    {
        var document = applicant.Documents.FirstOrDefault(d => d.Type == type);
        if (document is null)
        {
            return Error.NotFound("admissions.document_not_found", "The document was not uploaded.");
        }

        var extension = Path.GetExtension(document.FileKey).TrimStart('.');
        if (!MediaTypes.TryGetValue(extension, out var mediaType))
        {
            return Error.Validation("admissions.extract_needs_image", "The AI reads photos only. Upload the document as a photo (JPEG, PNG or WebP) to use it.");
        }

        var content = await storage.DownloadAsync(FileBucket.Private, document.FileKey, ct);
        if (content is null)
        {
            return Error.NotFound("admissions.document_not_found", "The document was not uploaded.");
        }

        if (content.Length > MaxImageBytes)
        {
            return Error.Validation("admissions.extract_too_large", "The photo is larger than 5 MB. Upload a smaller photo to use the AI.");
        }

        var completion = await gateway.RunAsync(userId, ExtractionPrompt.Build(type, new AiImage(mediaType, content, document.FileName)), ct);
        if (!completion.IsSuccess)
        {
            return completion.Error;
        }

        var fields = ExtractionPrompt.Parse(completion.Value.Text, clock.Today);
        if (!fields.IsSuccess)
        {
            return fields.Error;
        }

        var now = clock.UtcNow;
        document.SetExtraction(JsonSerializer.Serialize(fields.Value, Json), now);
        await db.SaveChangesAsync(ct);
        return new DocumentExtractionDto(type, now, fields.Value);
    }

    /// <summary>What was read from the documents so far, and what the documents and the form disagree on.</summary>
    public static ApplicantExtractionsDto Summarize(Applicant applicant)
    {
        var documents = applicant.Documents
            .Where(d => d.ExtractedData is not null && d.ExtractedAt is not null)
            .OrderBy(d => d.Type)
            .Select(d => new DocumentExtractionDto(d.Type, d.ExtractedAt!.Value, Read(d.ExtractedData!)))
            .ToList();
        return new ApplicantExtractionsDto(documents, ExtractionMismatches.Find(AdmissionSupport.ReadForm(applicant.FormData), documents));
    }

    private static ExtractedFieldsDto Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ExtractedFieldsDto>(json, Json) ?? ExtractedFieldsDto.Empty;
        }
        catch (JsonException)
        {
            return ExtractedFieldsDto.Empty;
        }
    }
}
