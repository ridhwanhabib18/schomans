using SchoMans.Domain.Admissions;

namespace SchoMans.Application.Features.Admissions.ExtractDocuments;

public sealed record ExtractedGradeDto(string Subject, decimal Score);

/// <summary>What the AI read from one document. Every field is empty when the document does not show it or it could not be read.</summary>
public sealed record ExtractedFieldsDto(
    string? FullName,
    string? Nik,
    DateOnly? DateOfBirth,
    string? PlaceOfBirth,
    string? Address,
    string? FatherName,
    string? MotherName,
    string? GuardianName,
    List<ExtractedGradeDto> Grades)
{
    public static readonly ExtractedFieldsDto Empty = new(null, null, null, null, null, null, null, null, []);

    public bool HasAnything =>
        FullName is not null || Nik is not null || DateOfBirth is not null || PlaceOfBirth is not null || Address is not null
        || FatherName is not null || MotherName is not null || GuardianName is not null || Grades.Count > 0;
}

public sealed record DocumentExtractionDto(DocumentType Type, DateTimeOffset ExtractedAt, ExtractedFieldsDto Fields);

/// <param name="Source"><c>Form</c> or the name of the document type.</param>
public sealed record ExtractionValueDto(string Source, string Value);

/// <summary>A field that the documents (and the form) do not agree on. For staff to look into; the AI never decides which is right.</summary>
public sealed record ExtractionMismatchDto(string Field, List<ExtractionValueDto> Values);

public sealed record ApplicantExtractionsDto(List<DocumentExtractionDto> Documents, List<ExtractionMismatchDto> Mismatches);
